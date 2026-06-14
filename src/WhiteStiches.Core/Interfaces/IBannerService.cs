using WhiteStiches.Core.Entities.Content;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Storefront read access to the homepage hero banner.</summary>
public interface IBannerService
{
    /// <summary>
    /// The single active hero banner (highest <c>SortOrder</c>) with its media and stats, or null
    /// when none is published — in which case the storefront falls back to its built-in hero.
    /// </summary>
    Task<Banner?> GetActiveHeroAsync(CancellationToken ct = default);
}
