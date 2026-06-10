using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Shop;

public class SearchViewModel
{
    public string? Query { get; init; }

    /// <summary>Null when no query was submitted.</summary>
    public PagedResult<Product>? Results { get; init; }

    /// <summary>Featured products shown when there is no query or zero results.</summary>
    public IReadOnlyList<Product> Suggestions { get; init; } = [];

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
    public int TotalCount => Results?.TotalCount ?? 0;
    public bool IsEmpty => HasQuery && TotalCount == 0;

    public string PageUrl(int page)
    {
        var url = $"/search?q={Uri.EscapeDataString(Query ?? string.Empty)}";
        return page > 1 ? $"{url}&page={page}" : url;
    }
}
