using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

/// <summary>
/// In-memory paging for small, bounded admin lists whose service returns the full set
/// (Staff, Static pages). Mirrors the <see cref="PagedResult{T}"/> shape the bigger,
/// DB-paged lists already use so the same <c>_Pager</c> partial renders unchanged.
/// </summary>
public static class PagingExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IReadOnlyList<T> source, int page, int pageSize)
    {
        var p = page < 1 ? 1 : page;
        return new PagedResult<T>
        {
            Items = source.Skip((p - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = source.Count,
            Page = p,
            PageSize = pageSize
        };
    }
}
