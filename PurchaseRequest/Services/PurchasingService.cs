using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Components.Shared;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    // FR-11: Purchasing moves approved requests to Ordered, then Received.
    public class PurchasingService(IDbContextFactory<ApplicationDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
    {
        // Approved requests waiting to be ordered, then ordered requests waiting to arrive, oldest first.
        public async Task<List<Request>> GetQueueAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return await db.Requests.AsNoTracking()
                .Include(r => r.Requester)
                .Include(r => r.Category)
                .Where(r => r.Status == RequestStatus.Approved || r.Status == RequestStatus.Ordered)
                .OrderBy(r => r.Status == RequestStatus.Approved ? 0 : 1)
                .ThenBy(r => r.SubmittedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<RequestResult> MarkOrderedAsync(int requestId, CurrentUser user, string? comment, CancellationToken cancellationToken = default) =>
            MoveAsync(requestId, user, RequestStatus.Approved, RequestStatus.Ordered, HistoryAction.Ordered, NotificationEvent.Ordered, comment, cancellationToken);

        public Task<RequestResult> MarkReceivedAsync(int requestId, CurrentUser user, string? comment, CancellationToken cancellationToken = default) =>
            MoveAsync(requestId, user, RequestStatus.Ordered, RequestStatus.Received, HistoryAction.Received, NotificationEvent.Received, comment, cancellationToken);

        public static bool CanMarkOrdered(Request request, CurrentUser user) =>
            user.IsInRole(AppRoles.Purchasing) && request.Status == RequestStatus.Approved;

        public static bool CanMarkReceived(Request request, CurrentUser user) =>
            user.IsInRole(AppRoles.Purchasing) && request.Status == RequestStatus.Ordered;

        private async Task<RequestResult> MoveAsync(
            int requestId,
            CurrentUser user,
            RequestStatus from,
            RequestStatus to,
            HistoryAction action,
            NotificationEvent notification,
            string? comment,
            CancellationToken cancellationToken)
        {
            if (!user.IsInRole(AppRoles.Purchasing))
            {
                return new(AccessResult.Denied);
            }
            comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var request = await db.Requests.SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request is null)
            {
                return new(AccessResult.NotFound);
            }
            if (request.Status != from)
            {
                throw new InvalidOperationException($"Only {Format.Status(from).ToLowerInvariant()} requests can be marked {to.ToString().ToLowerInvariant()}.");
            }

            request.Status = to;
            db.RequestHistory.Add(new RequestHistory
            {
                RequestId = request.Id,
                UserId = user.Id,
                Action = action,
                FromStatus = from,
                ToStatus = to,
                Comment = comment,
                OccurredAt = clock.GetUtcNow().UtcDateTime
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("Someone else already updated this request. Refresh the page to see the latest status.");
            }

            await notifications.NotifyAsync(request.Id, notification, comment, cancellationToken);
            return new(AccessResult.Allowed, request);
        }
    }
}
