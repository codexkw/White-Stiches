using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

/// <summary>Model for Views/Shared/_Pager.cshtml — preserves the current query string, swaps "page".</summary>
public record PagerInfo(int Page, int TotalPages)
{
    public static PagerInfo From<T>(PagedResult<T> result) => new(result.Page, result.TotalPages);
}
