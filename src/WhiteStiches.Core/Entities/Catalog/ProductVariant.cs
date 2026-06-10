namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>A purchasable option combination with its own SKU, price, and stock.</summary>
public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string? Sku { get; set; }
    public string? Barcode { get; set; }

    /// <summary>Value for the product's option at position 1 (e.g., Size "M").</summary>
    public string? Option1 { get; set; }
    public string? Option2 { get; set; }
    public string? Option3 { get; set; }

    /// <summary>Selling price in KWD (3 decimals).</summary>
    public decimal Price { get; set; }

    /// <summary>Strike-through original price when on sale.</summary>
    public decimal? CompareAtPrice { get; set; }

    /// <summary>Cost per item for margin tracking (staff only).</summary>
    public decimal? Cost { get; set; }

    public decimal? WeightKg { get; set; }

    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public bool AllowOversell { get; set; }

    public bool IsActive { get; set; } = true;
    public int Position { get; set; }

    /// <summary>Optional variant-specific image.</summary>
    public int? ImageId { get; set; }
    public ProductImage? Image { get; set; }

    public ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = [];
}
