using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>Back-office CRUD for homepage hero banners, their media, and stat counters.</summary>
public interface IBannerAdminService
{
    Task<PagedResult<BannerListItem>> GetListAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>Banner with its Images (by sort) and Stats (by sort). Null when missing.</summary>
    Task<Banner?> GetForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Creates (Id == 0) or updates the banner's own fields; the stat set is replaced wholesale.</summary>
    Task<Banner> SaveAsync(Banner banner, IReadOnlyList<BannerStat> stats, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Appends a background medium (next SortOrder). Null when the banner is missing.</summary>
    Task<BannerImage?> AddImageAsync(int bannerId, string url, MediaKind kind, CancellationToken ct = default);

    /// <summary>Deletes a banner medium. Returns its stored Url (to delete the file), or null when not found.</summary>
    Task<string?> DeleteImageAsync(int bannerId, int imageId, CancellationToken ct = default);

    /// <summary>Swaps a medium with its neighbour and re-sequences positions. False at the edge or when missing.</summary>
    Task<bool> MoveImageAsync(int bannerId, int imageId, bool moveUp, CancellationToken ct = default);

    /// <summary>Toggles IsActive. Returns the new state, or null when missing.</summary>
    Task<bool?> ToggleActiveAsync(int id, CancellationToken ct = default);
}
