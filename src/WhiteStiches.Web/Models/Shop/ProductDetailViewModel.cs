using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;

namespace WhiteStiches.Web.Models.Shop;

public class ProductDetailViewModel
{
    public required Product Product { get; init; }

    /// <summary>Related products — first 4 feed "You may also like", the rest "Complete the look".</summary>
    public IReadOnlyList<Product> Related { get; init; } = [];

    private List<ProductVariant>? _variants;
    public IReadOnlyList<ProductVariant> Variants =>
        _variants ??= Product.Variants
            .Where(v => v.IsActive)
            .OrderBy(v => v.Position)
            .ThenBy(v => v.Id)
            .ToList();

    public static bool IsPurchasable(ProductVariant v) => v.StockQuantity > 0 || v.AllowOversell;

    /// <summary>First in-stock variant, else the first variant (may be null when no variants exist).</summary>
    public ProductVariant? DefaultVariant =>
        Variants.FirstOrDefault(IsPurchasable) ?? Variants.FirstOrDefault();

    public IReadOnlyList<ProductImage> Images =>
        Product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();

    /// <summary>Distinct Option1 (size) values in variant order.</summary>
    public IReadOnlyList<string> SizeValues =>
        Variants.Select(v => v.Option1)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Distinct Option2 (color) values in variant order.</summary>
    public IReadOnlyList<string> ColorValues =>
        Variants.Select(v => v.Option2)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool IsSizeSoldOut(string size) =>
        Variants.Where(v => string.Equals(v.Option1?.Trim(), size, StringComparison.OrdinalIgnoreCase))
            .All(v => !IsPurchasable(v));

    public bool IsColorSoldOut(string color) =>
        Variants.Where(v => string.Equals(v.Option2?.Trim(), color, StringComparison.OrdinalIgnoreCase))
            .All(v => !IsPurchasable(v));

    /// <summary>
    /// Still photo to use as the color swatch — prefers a color-matched photo, then any photo, then the
    /// first product image. Videos are skipped as swatches (a clip makes a poor thumbnail).
    /// </summary>
    public ProductImage? ImageForColor(string color) =>
        Images.FirstOrDefault(i => i.MediaKind == MediaKind.Image
            && string.Equals(i.ColorName?.Trim(), color, StringComparison.OrdinalIgnoreCase))
        ?? Images.FirstOrDefault(i => i.MediaKind == MediaKind.Image)
        ?? Images.FirstOrDefault();

    public string SizeLabel =>
        Product.Options.FirstOrDefault(o => o.Position == 1)?.NameEn is { Length: > 0 } name ? name : "Size";

    public string ColorLabel =>
        Product.Options.FirstOrDefault(o => o.Position == 2)?.NameEn is { Length: > 0 } name ? name : "Colour";
}
