using System.Globalization;
using PurchaseRequest.Data;

namespace PurchaseRequest.Components.Shared
{
    public static class Format
    {
        private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

        public static string Money(decimal value) => value.ToString("C", UsCulture);

        public static string Date(DateTime utc) => utc.ToLocalTime().ToString("MMM d, yyyy", UsCulture);

        public static string DateAndTime(DateTime utc) => utc.ToLocalTime().ToString("MMM d, yyyy h:mm tt", UsCulture);

        public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", UsCulture);

        public static string Step(StepType stepType) => stepType switch
        {
            StepType.Manager => "Manager",
            StepType.IT => "IT",
            StepType.Finance => "Finance",
            _ => stepType.ToString()
        };

        // Who a step is waiting on: a named manager, or everyone in a role.
        public static string Approver(ApprovalStep step) => step.Approver?.DisplayName ?? step.ApproverRole switch
        {
            "ITApprover" => "IT approvers",
            "FinanceApprover" => "Finance approvers",
            "Admin" => "Admin (no manager in Entra ID)",
            var role => role ?? "—"
        };

        public static string Status(RequestStatus status) => status switch
        {
            RequestStatus.PendingApproval => "Pending approval",
            _ => status.ToString()
        };
    }
}
