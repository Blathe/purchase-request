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

        public int? ManagerId { get; set; }
        public AppUser? Manager { get; set; }
    }
}
