using System.Globalization;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Shop;

/// <summary>An active filter rendered as a removable chip on the collection sidebar.</summary>
public record FilterChip(string Label, string RemoveUrl);

public class CollectionViewModel
{
    public required PagedResult<Product> Products { get; init; }
    public IReadOnlyList<Category> Categories { get; init; } = [];

    /// <summary>Matched category NameEn / collection title, or "All" when browsing everything.</summary>
    public string BannerTitle { get; init; } = "All";

    /// <summary>When set, this is a collection page (/collections/{slug}); filters/paging stay within it.</summary>
    public string? CollectionSlug { get; init; }

    /// <summary>Base path for the filter form, paging, and clear links — collection route or the global catalog.</summary>
    public string BasePath => CollectionSlug is null
        ? "/collection"
        : $"/collections/{Uri.EscapeDataString(CollectionSlug)}";

    // Current filter state (echoes the query string)
    public string? Category { get; init; }
    public string? Size { get; init; }
    public string? Color { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public bool InStock { get; init; }
    public string Sort { get; init; } = "featured";

    /// <summary>Filter sidebar options sourced from real variant data (size = Option1, colour = Option2).</summary>
    public IReadOnlyList<string> SizeOptions { get; init; } = [];
    public IReadOnlyList<string> ColorOptions { get; init; } = [];

    public int ActiveFilterCount =>
        (Category is null ? 0 : 1) +
        (Size is null ? 0 : 1) +
        (Color is null ? 0 : 1) +
        (Min is null && Max is null ? 0 : 1) +
        (InStock ? 1 : 0);

    public IReadOnlyList<FilterChip> Chips
    {
        get
        {
            var chips = new List<FilterChip>();
            if (Category is not null) chips.Add(new FilterChip(BannerTitle, RemoveCategoryUrl()));
            if (Size is not null) chips.Add(new FilterChip(Size, RemoveSizeUrl()));
            if (Color is not null) chips.Add(new FilterChip(Color, RemoveColorUrl()));
            if (Min is not null || Max is not null) chips.Add(new FilterChip(PriceChipLabel(), RemovePriceUrl()));
            if (InStock) chips.Add(new FilterChip("In stock", RemoveInStockUrl()));
            return chips;
        }
    }

    public string PageUrl(int page) => BuildUrl(Category, Size, Color, Min, Max, InStock, Sort, page);
    public string RemoveCategoryUrl() => BuildUrl(null, Size, Color, Min, Max, InStock, Sort, 1);
    public string RemoveSizeUrl() => BuildUrl(Category, null, Color, Min, Max, InStock, Sort, 1);
    public string RemoveColorUrl() => BuildUrl(Category, Size, null, Min, Max, InStock, Sort, 1);
    public string RemovePriceUrl() => BuildUrl(Category, Size, Color, null, null, InStock, Sort, 1);
    public string RemoveInStockUrl() => BuildUrl(Category, Size, Color, Min, Max, false, Sort, 1);

    private string PriceChipLabel()
    {
        static string F(decimal v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        return (Min, Max) switch
        {
            (not null, not null) => $"{F(Min.Value)} – {F(Max.Value)} KWD",
            (not null, null) => $"Over {F(Min.Value)} KWD",
            (null, not null) => $"Under {F(Max.Value)} KWD",
            _ => string.Empty
        };
    }

    private string BuildUrl(
        string? category, string? size, string? color,
        decimal? min, decimal? max, bool instock, string? sort, int page)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }

        Add("category", category);
        Add("size", size);
        Add("color", color);
        if (min is not null) parts.Add($"min={min.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (max is not null) parts.Add($"max={max.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (instock) parts.Add("instock=true");
        if (!string.IsNullOrEmpty(sort) && !string.Equals(sort, "featured", StringComparison.OrdinalIgnoreCase)) Add("sort", sort);
        if (page > 1) parts.Add($"page={page}");

        return parts.Count == 0 ? BasePath : BasePath + "?" + string.Join('&', parts);
    }
}
