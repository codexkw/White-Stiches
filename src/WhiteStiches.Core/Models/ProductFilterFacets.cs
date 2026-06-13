namespace WhiteStiches.Core.Models;

/// <summary>
/// Distinct filter option values (sizes, colours) for the collection filter sidebar, computed from
/// the actual variant data so every swatch maps to products that really exist — replacing the old
/// hardcoded lists that offered values no product had (and hid colours that products did have).
/// </summary>
public class ProductFilterFacets
{
    public IReadOnlyList<string> Sizes { get; init; } = [];
    public IReadOnlyList<string> Colors { get; init; } = [];
}
