using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    public record EmailMessage(string To, string Subject, string Body);

    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    // Development sender: writes each email to the log and the Outbox table instead of sending it.
    // Swap for an SMTP or Microsoft Graph sender without changing the workflow code.
    public class LogEmailSender(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider clock, ILogger<LogEmailSender> logger) : IEmailSender
    {
        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Email to {To}: {Subject}", message.To, message.Subject);

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.OutboxMessages.Add(new OutboxMessage
            {
                To = message.To,
                Subject = message.Subject,
                Body = message.Body,
                CreatedAt = clock.GetUtcNow().UtcDateTime
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
