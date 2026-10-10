namespace PurchaseRequest.Data
{
    // A purchase request. Named Request because PurchaseRequest is the root namespace.
    public class Request
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";

        public int RequesterId { get; set; }
        public AppUser Requester { get; set; } = null!;

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public string Vendor { get; set; } = "";
        public string Justification { get; set; } = "";
        public DateOnly? NeededBy { get; set; }
        public RequestStatus Status { get; set; } = RequestStatus.Draft;
        public decimal Total { get; set; }

        // BR-02: the finance threshold in force when the request was submitted.
        public decimal? ThresholdAtSubmit { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public List<LineItem> LineItems { get; set; } = [];
        public List<ApprovalStep> ApprovalSteps { get; set; } = [];
        public List<RequestHistory> History { get; set; } = [];
    }
}
