namespace PurchaseRequest.Data
{
    public class LineItem
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string Description { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string? Url { get; set; }

        public decimal LineTotal => Quantity * UnitPrice;
    }
}
