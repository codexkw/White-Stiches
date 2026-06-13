using WhiteStiches.Core.Entities.Catalog;

namespace WhiteStiches.Web.Models.Shop;

/// <summary>
/// Payload for the header search overlay's live suggestions (GET /search/suggest), rendered as a
/// small HTML partial that site.js injects into #searchOverlayResults. Backed by the same catalog
/// search the full /search page uses.
/// </summary>
public class SearchSuggestViewModel
{
    public string? Query { get; init; }

    /// <summary>The top matches to show in the overlay (capped small).</summary>
    public IReadOnlyList<Product> Products { get; init; } = [];

    /// <summary>Total matches across the whole catalog (may exceed <see cref="Products"/> count).</summary>
    public int TotalCount { get; init; }
}
