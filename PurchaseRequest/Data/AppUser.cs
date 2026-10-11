namespace PurchaseRequest.Data
{
    // Local copy of an Entra ID user, refreshed at each sign-in.
    public class AppUser
    {
        public int Id { get; set; }
        public Guid EntraObjectId { get; set; }
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Department { get; set; }

        // App roles from the last sign-in, stored as ",Role1,Role2," so a role can be matched with Contains.
        public string Roles { get; set; } = ",";

        public int? ManagerId { get; set; }
        public AppUser? Manager { get; set; }
    }
}
