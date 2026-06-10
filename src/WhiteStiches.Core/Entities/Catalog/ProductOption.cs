namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>A product option axis (Size, Color, Fit, Length). Position maps to ProductVariant.Option1/2/3.</summary>
public class ProductOption : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    /// <summary>1-based position: 1 → Variant.Option1, 2 → Variant.Option2, 3 → Variant.Option3.</summary>
    public int Position { get; set; } = 1;

    /// <summary>Comma-separated display-ordered values (e.g., "XS,S,M,L,XL").</summary>
    public string? ValuesCsv { get; set; }
}
