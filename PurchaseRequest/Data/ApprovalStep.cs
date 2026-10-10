namespace PurchaseRequest.Data
{
    public class ApprovalStep
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int Sequence { get; set; }
        public StepType StepType { get; set; }

        // Manager steps go to one person. IT and Finance steps go to anyone in the role.
        public int? ApproverId { get; set; }
        public AppUser? Approver { get; set; }
        public string? ApproverRole { get; set; }

        public Decision Decision { get; set; } = Decision.Pending;

        // Who actually decided. For role-based steps this is set when the decision is made.
        public int? DecidedById { get; set; }
        public AppUser? DecidedBy { get; set; }
        public string? Comment { get; set; }
        public DateTime? DecidedAt { get; set; }
    }
}
