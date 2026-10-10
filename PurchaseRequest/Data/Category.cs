namespace PurchaseRequest.Data
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        // BR-03: requests in this category need IT approval after the manager.
        public bool RequiresItApproval { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
