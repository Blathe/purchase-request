namespace PurchaseRequest.Data
{
    // Emails recorded by the log-only email sender, shown on the Outbox page.
    public class OutboxMessage
    {
        public long Id { get; set; }
        public string To { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
