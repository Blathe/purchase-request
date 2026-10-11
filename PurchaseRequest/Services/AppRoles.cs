namespace PurchaseRequest.Services
{
    // App role values defined in the Entra ID app registration.
    public static class AppRoles
    {
        public const string ITApprover = "ITApprover";
        public const string FinanceApprover = "FinanceApprover";
        public const string Purchasing = "Purchasing";
        public const string Admin = "Admin";

        public static readonly string[] All = [ITApprover, FinanceApprover, Purchasing, Admin];

        // Roles that can view every request (NFR-02).
        public static readonly string[] ViewAll = [FinanceApprover, Purchasing, Admin];

        public static string ToStored(IEnumerable<string> roles) => "," + string.Join(",", roles.Order()) + (roles.Any() ? "," : "");

        public static string StoredPattern(string role) => "," + role + ",";
    }

    // The signed-in user as the services see them.
    public sealed record CurrentUser(int Id, string DisplayName, IReadOnlySet<string> Roles)
    {
        public bool IsInRole(string role) => Roles.Contains(role);

        public bool CanViewAll => AppRoles.ViewAll.Any(IsInRole);
    }
}
