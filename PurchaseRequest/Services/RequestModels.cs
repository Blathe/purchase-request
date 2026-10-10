using System.ComponentModel.DataAnnotations;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    // Form model for creating and editing a draft (FR-02).
    public class RequestFormModel
    {
        [Required, StringLength(200)]
        public string Vendor { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
        public int CategoryId { get; set; }

        [Required, StringLength(2000)]
        public string Justification { get; set; } = "";

        public DateOnly? NeededBy { get; set; }

        public List<LineItemModel> LineItems { get; set; } = [new()];

        // FR-03: totals are always calculated, never entered.
        public decimal Total => LineItems.Sum(l => l.LineTotal);

        public static RequestFormModel FromRequest(Request request) => new()
        {
            Vendor = request.Vendor,
            CategoryId = request.CategoryId,
            Justification = request.Justification,
            NeededBy = request.NeededBy,
            LineItems = request.LineItems.Select(l => new LineItemModel
            {
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Url = l.Url
            }).ToList()
        };
    }

    public class LineItemModel
    {
        [Required, StringLength(500)]
        public string Description { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be 1 or more.")]
        public int Quantity { get; set; } = 1;

        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Unit price must be at least $0.01.")]
        public decimal UnitPrice { get; set; }

        [Url, StringLength(1000)]
        public string? Url { get; set; }

        public decimal LineTotal => Quantity * UnitPrice;
    }

    public enum AccessResult
    {
        Allowed,
        NotFound,
        Denied
    }

    public record RequestResult(AccessResult Access, Request? Request = null);
}
