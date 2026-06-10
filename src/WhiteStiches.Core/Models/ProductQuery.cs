using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Models;

/// <summary>Storefront/admin product listing query: filters, sort, paging (SF-COL-02/03).</summary>
public class ProductQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? CategorySlug { get; set; }
    public string? CollectionSlug { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public string? Tag { get; set; }
    public bool InStockOnly { get; set; }

    /// <summary>Admin listings include drafts/archived; storefront sees Active only.</summary>
    public ProductStatus? Status { get; set; } = ProductStatus.Active;

    public ProductSort Sort { get; set; } = ProductSort.Featured;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public enum ProductSort
{
    Featured = 0,
    Newest = 1,
    BestSelling = 2,
    PriceLowToHigh = 3,
    PriceHighToLow = 4,
    Alphabetical = 5
}
