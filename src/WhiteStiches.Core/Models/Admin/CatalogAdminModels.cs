namespace WhiteStiches.Core.Models.Admin;

/// <summary>
/// One option axis submitted from the admin product editor (e.g., Name "Size",
/// ValuesCsv "XS,S,M,L"). Blank rows are ignored by the service.
/// </summary>
public record ProductOptionInput(string Name, string ValuesCsv);

/// <summary>
/// One row of the spreadsheet-style variant editor (AD-PRD-02). Mutable properties
/// so MVC can bind indexed form fields (variants[0].Id, variants[0].Price, ...).
/// </summary>
public class VariantUpdateRow
{
    public int Id { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string? Sku { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public bool AllowOversell { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Product image assigned to this variant (null = none). Must belong to the same product.</summary>
    public int? ImageId { get; set; }
}
