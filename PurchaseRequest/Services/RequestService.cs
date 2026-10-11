using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    public class RequestService(IDbContextFactory<ApplicationDbContext> dbFactory, IRequestNumberGenerator numberGenerator, TimeProvider clock)
    {
        public async Task<List<Category>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return await db.Categories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        // FR-09: requesters see only their own requests (NFR-02).
        public async Task<List<Request>> GetMyRequestsAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return await db.Requests.AsNoTracking()
                .Include(r => r.Category)
                .Include(r => r.ApprovalSteps).ThenInclude(s => s.Approver)
                .Where(r => r.RequesterId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<RequestResult> GetForViewAsync(int requestId, CurrentUser user, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var request = await db.Requests.AsNoTracking()
                .Include(r => r.Category)
                .Include(r => r.Requester)
                .Include(r => r.LineItems.OrderBy(l => l.Id))
                .Include(r => r.History.OrderBy(h => h.OccurredAt)).ThenInclude(h => h.User)
                .Include(r => r.ApprovalSteps.OrderBy(s => s.Round).ThenBy(s => s.Sequence)).ThenInclude(s => s.Approver)
                .Include(r => r.ApprovalSteps).ThenInclude(s => s.DecidedBy)
                .AsSplitQuery()
                .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request is null)
            {
                return new(AccessResult.NotFound);
            }

            return CanView(request, user) ? new(AccessResult.Allowed, request) : new(AccessResult.Denied);
        }

        // NFR-02: the requester, anyone assigned to or holding the role for one of its steps,
        // and Finance, Purchasing and Admin can view a request.
        public static bool CanView(Request request, CurrentUser user) =>
            request.RequesterId == user.Id
            || user.CanViewAll
            || request.ApprovalSteps.Any(s => s.ApproverId == user.Id
                || s.DecidedById == user.Id
                || (s.ApproverRole is not null && user.IsInRole(s.ApproverRole)));

        // FR-02, FR-03, FR-04: create a new draft and assign its number.
        public async Task<Request> CreateDraftAsync(int userId, RequestFormModel model, CancellationToken cancellationToken = default)
        {
            Validate(model);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = clock.GetUtcNow().UtcDateTime;
            var request = new Request
            {
                Number = await numberGenerator.NextAsync(db, cancellationToken),
                RequesterId = userId,
                Status = RequestStatus.Draft,
                CreatedAt = now
            };
            Apply(model, request);
            request.History.Add(new RequestHistory
            {
                UserId = userId,
                Action = HistoryAction.Created,
                ToStatus = RequestStatus.Draft,
                OccurredAt = now
            });

            db.Requests.Add(request);
            await db.SaveChangesAsync(cancellationToken);
            return request;
        }

        // FR-04: drafts can be edited by their requester until submitted (BR-05).
        public async Task<RequestResult> UpdateDraftAsync(int requestId, int userId, RequestFormModel model, CancellationToken cancellationToken = default)
        {
            Validate(model);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var request = await db.Requests
                .Include(r => r.LineItems)
                .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request is null)
            {
                return new(AccessResult.NotFound);
            }
            if (request.RequesterId != userId)
            {
                return new(AccessResult.Denied);
            }
            if (!CanEdit(request))
            {
                throw new InvalidOperationException("Only draft requests can be edited.");
            }

            db.LineItems.RemoveRange(request.LineItems);
            request.LineItems.Clear();
            Apply(model, request);
            await db.SaveChangesAsync(cancellationToken);
            return new(AccessResult.Allowed, request);
        }

        // FR-12: the requester can cancel before final approval.
        public async Task<RequestResult> CancelAsync(int requestId, int userId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var request = await db.Requests
                .Include(r => r.ApprovalSteps)
                .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request is null)
            {
                return new(AccessResult.NotFound);
            }
            if (request.RequesterId != userId)
            {
                return new(AccessResult.Denied);
            }
            if (!CanCancel(request))
            {
                throw new InvalidOperationException("Only draft or pending requests can be cancelled.");
            }

            db.RequestHistory.Add(new RequestHistory
            {
                RequestId = request.Id,
                UserId = userId,
                Action = HistoryAction.Cancelled,
                FromStatus = request.Status,
                ToStatus = RequestStatus.Cancelled,
                OccurredAt = clock.GetUtcNow().UtcDateTime
            });
            ApprovalService.SupersedePendingSteps(request);
            request.Status = RequestStatus.Cancelled;
            await db.SaveChangesAsync(cancellationToken);
            return new(AccessResult.Allowed, request);
        }

        public static bool CanEdit(Request request) => request.Status == RequestStatus.Draft;

        public static bool CanCancel(Request request) =>
            request.Status is RequestStatus.Draft or RequestStatus.PendingApproval;

        private static void Apply(RequestFormModel model, Request request)
        {
            request.Vendor = model.Vendor.Trim();
            request.CategoryId = model.CategoryId;
            request.Justification = model.Justification.Trim();
            request.NeededBy = model.NeededBy;
            request.LineItems.AddRange(model.LineItems.Select(l => new LineItem
            {
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Url = string.IsNullOrWhiteSpace(l.Url) ? null : l.Url.Trim()
            }));
            request.Total = model.Total;
        }

        // The form validates too; this guards against callers that skip it.
        private static void Validate(RequestFormModel model)
        {
            Validator.ValidateObject(model, new ValidationContext(model), validateAllProperties: true);
            if (model.LineItems.Count == 0)
            {
                throw new ValidationException("A request needs at least one line item.");
            }
            foreach (var line in model.LineItems)
            {
                Validator.ValidateObject(line, new ValidationContext(line), validateAllProperties: true);
            }
        }
    }
}
