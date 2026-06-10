namespace WhiteStiches.Core.Entities.Orders;

/// <summary>Order line with full product snapshot. Variant FK is nullable so catalog deletions never break history.</summary>
public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int? ProductId { get; set; }
    public int? ProductVariantId { get; set; }

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Human-readable variant summary (e.g., "Black / M").</summary>
    public string? VariantDescription { get; set; }

    public string? Sku { get; set; }
    public string? ImageUrl { get; set; }

    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    public int FulfilledQuantity { get; set; }
}
