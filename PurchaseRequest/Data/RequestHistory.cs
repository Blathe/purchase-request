namespace PurchaseRequest.Data
{
    // Append-only audit trail (NFR-03). ApplicationDbContext rejects updates and deletes.
    public class RequestHistory
    {
        public long Id { get; set; }
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public AppUser User { get; set; } = null!;
        public HistoryAction Action { get; set; }
        public RequestStatus? FromStatus { get; set; }
        public RequestStatus ToStatus { get; set; }
        public string? Comment { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
