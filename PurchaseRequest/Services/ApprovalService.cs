using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    public record ApprovalQueueItem(int RequestId, string Number, string Requester, string Vendor, decimal Total, StepType StepType, DateTime WaitingSince);

    public class ApprovalService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IDirectoryService directory,
        NotificationService notifications,
        TimeProvider clock,
        ILogger<ApprovalService> logger)
    {
        public const decimal DefaultFinanceThreshold = 5000.00m;

        // FR-05: submit a draft and build its approval chain.
        public async Task<RequestResult> SubmitAsync(int requestId, int userId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var request = await db.Requests
                .Include(r => r.Category)
                .Include(r => r.Requester)
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
            if (request.Status != RequestStatus.Draft)
            {
                throw new InvalidOperationException("Only draft requests can be submitted.");
            }
            if (request.Total <= 0)
            {
                throw new InvalidOperationException("Add at least one line item before submitting.");
            }

            var manager = await ResolveManagerAsync(db, request.Requester, cancellationToken);
            var threshold = await GetFinanceThresholdAsync(db, cancellationToken);
            var round = (request.ApprovalSteps.Max(s => (int?)s.Round) ?? 0) + 1;
            var now = clock.GetUtcNow().UtcDateTime;

            request.ApprovalSteps.AddRange(ApprovalChainBuilder.Build(round, manager, request.Category, request.Total, threshold));
            request.ThresholdAtSubmit = threshold;
            request.SubmittedAt = now;
            request.Status = RequestStatus.PendingApproval;
            request.History.Add(new RequestHistory
            {
                UserId = userId,
                Action = HistoryAction.Submitted,
                FromStatus = RequestStatus.Draft,
                ToStatus = RequestStatus.PendingApproval,
                OccurredAt = now
            });

            await db.SaveChangesAsync(cancellationToken);
            await notifications.NotifyAsync(request.Id, NotificationEvent.AwaitingApproval, cancellationToken: cancellationToken);
            return new(AccessResult.Allowed, request);
        }

        // FR-07: approve, reject or return the current step.
        public async Task<RequestResult> DecideAsync(int requestId, CurrentUser user, Decision decision, string? comment, CancellationToken cancellationToken = default)
        {
            if (decision is not (Decision.Approved or Decision.Rejected or Decision.Returned))
            {
                throw new ArgumentOutOfRangeException(nameof(decision));
            }
            comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            if (decision != Decision.Approved && comment is null)
            {
                throw new ValidationException("A comment is required.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var request = await db.Requests
                .Include(r => r.ApprovalSteps)
                .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request is null)
            {
                return new(AccessResult.NotFound);
            }
            if (request.Status != RequestStatus.PendingApproval)
            {
                throw new InvalidOperationException("This request is no longer waiting for approval.");
            }

            var step = CurrentStep(request);
            if (!CanDecide(request, step, user))
            {
                return new(AccessResult.Denied);
            }

            var now = clock.GetUtcNow().UtcDateTime;
            var fromStatus = request.Status;
            step!.Decision = decision;
            step.DecidedById = user.Id;
            step.Comment = comment;
            step.DecidedAt = now;

            NotificationEvent notification;
            if (decision == Decision.Approved)
            {
                var finalStep = CurrentStep(request) is null;
                request.Status = finalStep ? RequestStatus.Approved : RequestStatus.PendingApproval;
                notification = finalStep ? NotificationEvent.Approved : NotificationEvent.StepApproved;
            }
            else
            {
                // BR-05: a returned request goes back to Draft; resubmitting starts a new round.
                SupersedePendingSteps(request);
                request.Status = decision == Decision.Rejected ? RequestStatus.Rejected : RequestStatus.Draft;
                notification = decision == Decision.Rejected ? NotificationEvent.Rejected : NotificationEvent.Returned;
            }

            db.RequestHistory.Add(new RequestHistory
            {
                RequestId = request.Id,
                UserId = user.Id,
                Action = decision switch
                {
                    Decision.Approved => HistoryAction.Approved,
                    Decision.Rejected => HistoryAction.Rejected,
                    _ => HistoryAction.Returned
                },
                FromStatus = fromStatus,
                ToStatus = request.Status,
                Comment = comment,
                OccurredAt = now
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("Someone else already decided this step. Refresh the page to see the latest status.");
            }

            await notifications.NotifyAsync(request.Id, notification, comment, cancellationToken);
            if (notification == NotificationEvent.StepApproved)
            {
                await notifications.NotifyAsync(request.Id, NotificationEvent.AwaitingApproval, cancellationToken: cancellationToken);
            }
            return new(AccessResult.Allowed, request);
        }

        // FR-06: requests whose current step is waiting on this user, oldest first.
        public async Task<List<ApprovalQueueItem>> GetQueueAsync(CurrentUser user, CancellationToken cancellationToken = default)
        {
            var roles = user.Roles.ToList();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var items = await (
                from step in db.ApprovalSteps
                join request in db.Requests on step.RequestId equals request.Id
                where step.Decision == Decision.Pending
                    && request.Status == RequestStatus.PendingApproval
                    && request.RequesterId != user.Id
                    && (step.ApproverId == user.Id || (step.ApproverRole != null && roles.Contains(step.ApproverRole)))
                    && !db.ApprovalSteps.Any(earlier => earlier.RequestId == step.RequestId
                        && earlier.Decision == Decision.Pending
                        && earlier.Round == step.Round
                        && earlier.Sequence < step.Sequence)
                select new ApprovalQueueItem(
                    request.Id,
                    request.Number,
                    request.Requester.DisplayName,
                    request.Vendor,
                    request.Total,
                    step.StepType,
                    db.ApprovalSteps
                        .Where(previous => previous.RequestId == step.RequestId && previous.Round == step.Round && previous.DecidedAt != null)
                        .Max(previous => previous.DecidedAt) ?? request.SubmittedAt ?? request.CreatedAt))
                .ToListAsync(cancellationToken);

            return items.OrderBy(i => i.WaitingSince).ToList();
        }

        public static ApprovalStep? CurrentStep(Request request) =>
            request.ApprovalSteps
                .Where(s => s.Decision == Decision.Pending)
                .OrderBy(s => s.Round)
                .ThenBy(s => s.Sequence)
                .FirstOrDefault();

        // BR-04: nobody decides on their own request, even if they hold the step's role.
        public static bool CanDecide(Request request, ApprovalStep? step, CurrentUser user) =>
            request.Status == RequestStatus.PendingApproval
            && step is not null
            && request.RequesterId != user.Id
            && (step.ApproverId == user.Id || (step.ApproverRole is not null && user.IsInRole(step.ApproverRole)));

        public static void SupersedePendingSteps(Request request)
        {
            foreach (var pending in request.ApprovalSteps.Where(s => s.Decision == Decision.Pending))
            {
                pending.Decision = Decision.Superseded;
            }
        }

        // BR-01: look up the manager in Entra ID at submit time so the chain uses the current org chart.
        private async Task<AppUser?> ResolveManagerAsync(ApplicationDbContext db, AppUser requester, CancellationToken cancellationToken)
        {
            try
            {
                var profile = await directory.GetUserAsync(requester.EntraObjectId, cancellationToken);
                if (profile is not null)
                {
                    requester.Department = profile.Department;
                }

                var manager = await directory.GetManagerAsync(requester.EntraObjectId, cancellationToken);
                if (manager is null || manager.Id == requester.EntraObjectId)
                {
                    requester.Manager = null;
                    requester.ManagerId = null;
                    return null;
                }

                var managerUser = await db.Users.SingleOrDefaultAsync(u => u.EntraObjectId == manager.Id, cancellationToken);
                if (managerUser is null)
                {
                    managerUser = new AppUser { EntraObjectId = manager.Id };
                    db.Users.Add(managerUser);
                }
                managerUser.DisplayName = manager.DisplayName;
                managerUser.Email = manager.Email;
                managerUser.Department = manager.Department;
                requester.Manager = managerUser;
                return managerUser;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fall back to the last known manager so a Graph outage does not block submitting.
                logger.LogWarning(ex, "Could not look up the manager for user {UserId} in Microsoft Graph.", requester.Id);
                return requester.ManagerId is int managerId
                    ? await db.Users.FindAsync([managerId], cancellationToken)
                    : null;
            }
        }

        private static async Task<decimal> GetFinanceThresholdAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            var value = await db.Settings
                .Where(s => s.Key == Setting.FinanceThreshold)
                .Select(s => s.Value)
                .SingleOrDefaultAsync(cancellationToken);
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold)
                ? threshold
                : DefaultFinanceThreshold;
        }
    }
}
