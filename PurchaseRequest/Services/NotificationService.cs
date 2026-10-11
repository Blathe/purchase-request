using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Components.Shared;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    public enum NotificationEvent
    {
        AwaitingApproval,
        StepApproved,
        Approved,
        Rejected,
        Returned,
        Ordered,
        Received
    }

    // FR-08, FR-11: emails approvers when a step is waiting on them and the requester on every decision and delivery update.
    // Failures are logged, never thrown: a lost email must not undo an approval.
    public class NotificationService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        private string BaseUrl => (configuration["App:BaseUrl"] ?? "https://localhost:7087/").TrimEnd('/') + "/";

        public async Task NotifyAsync(int requestId, NotificationEvent notification, string? comment = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                var request = await db.Requests.AsNoTracking()
                    .Include(r => r.Requester)
                    .Include(r => r.ApprovalSteps).ThenInclude(s => s.Approver)
                    .SingleAsync(r => r.Id == requestId, cancellationToken);

                var link = $"{BaseUrl}requests/{request.Id}";
                var summary = $"{request.Number}: {Format.Money(request.Total)} from {request.Vendor}, requested by {request.Requester.DisplayName}.";
                var commentLine = comment is null ? "" : $"\n\nComment: {comment}";

                switch (notification)
                {
                    case NotificationEvent.AwaitingApproval:
                        var step = ApprovalService.CurrentStep(request);
                        if (step is null)
                        {
                            return;
                        }
                        foreach (var to in await ApproverEmailsAsync(db, request, step, cancellationToken))
                        {
                            await SendAsync(to, $"Approval needed: {request.Number}", $"{summary}\n\nReview it here: {link}", cancellationToken);
                        }
                        break;

                    case NotificationEvent.StepApproved:
                        await SendAsync(request.Requester.Email, $"Step approved: {request.Number}",
                            $"{summary}\n\nOne approval step is complete. It is now waiting on the next approver.{commentLine}\n\n{link}", cancellationToken);
                        break;

                    case NotificationEvent.Approved:
                        await SendAsync(request.Requester.Email, $"Approved: {request.Number}",
                            $"{summary}\n\nYour request is fully approved and has been sent to Purchasing.{commentLine}\n\n{link}", cancellationToken);
                        foreach (var to in await RoleEmailsAsync(db, AppRoles.Purchasing, request.RequesterId, cancellationToken))
                        {
                            await SendAsync(to, $"Ready to order: {request.Number}", $"{summary}\n\n{link}", cancellationToken);
                        }
                        break;

                    case NotificationEvent.Rejected:
                        await SendAsync(request.Requester.Email, $"Rejected: {request.Number}", $"{summary}{commentLine}\n\n{link}", cancellationToken);
                        break;

                    case NotificationEvent.Returned:
                        await SendAsync(request.Requester.Email, $"Returned for changes: {request.Number}",
                            $"{summary}{commentLine}\n\nEdit and resubmit it here: {link}", cancellationToken);
                        break;

                    // FR-11
                    case NotificationEvent.Ordered:
                        await SendAsync(request.Requester.Email, $"Ordered: {request.Number}",
                            $"{summary}\n\nPurchasing has placed the order.{commentLine}\n\n{link}", cancellationToken);
                        break;

                    case NotificationEvent.Received:
                        await SendAsync(request.Requester.Email, $"Received: {request.Number}",
                            $"{summary}\n\nYour order has arrived.{commentLine}\n\n{link}", cancellationToken);
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not send {Notification} notification for request {RequestId}.", notification, requestId);
            }
        }

        private static async Task<List<string>> ApproverEmailsAsync(ApplicationDbContext db, Request request, ApprovalStep step, CancellationToken cancellationToken) =>
            step.Approver is { } approver
                ? [approver.Email]
                : await RoleEmailsAsync(db, step.ApproverRole!, request.RequesterId, cancellationToken);

        // Role members are known from their last sign-in, so someone who has never signed in is not emailed.
        private static Task<List<string>> RoleEmailsAsync(ApplicationDbContext db, string role, int excludeUserId, CancellationToken cancellationToken)
        {
            var pattern = AppRoles.StoredPattern(role);
            return db.Users.AsNoTracking()
                .Where(u => u.Roles.Contains(pattern) && u.Id != excludeUserId && u.Email != "")
                .Select(u => u.Email)
                .ToListAsync(cancellationToken);
        }

        private Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) =>
            string.IsNullOrEmpty(to) ? Task.CompletedTask : emailSender.SendAsync(new EmailMessage(to, subject, body), cancellationToken);
    }
}
