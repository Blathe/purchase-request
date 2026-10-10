namespace PurchaseRequest.Data
{
    public enum RequestStatus
    {
        Draft,
        PendingApproval,
        Approved,
        Rejected,
        Ordered,
        Received,
        Cancelled
    }

    public enum StepType
    {
        Manager,
        IT,
        Finance
    }

    public enum Decision
    {
        Pending,
        Approved,
        Rejected,
        Returned,
        Superseded
    }

    public enum HistoryAction
    {
        Created,
        Submitted,
        Approved,
        Rejected,
        Returned,
        Cancelled,
        Ordered,
        Received
    }
}
