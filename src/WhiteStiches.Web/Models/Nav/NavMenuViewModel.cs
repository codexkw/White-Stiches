using WhiteStiches.Core.Entities.Catalog;

namespace WhiteStiches.Web.Models.Nav;

/// <summary>
/// Drives every storefront navigation surface (header links, mega-menu panels, mobile
/// drawer, footer "Shop" column, homepage category circles) from the live catalog so
/// admin-managed categories and collections appear automatically — no hardcoded slugs.
/// </summary>
public class NavMenuViewModel
{
    /// <summary>Active root categories (with their active children), ordered by SortOrder.</summary>
    public IReadOnlyList<Category> Categories { get; init; } = [];

    /// <summary>Active collections, ordered by title.</summary>
    public IReadOnlyList<Collection> Collections { get; init; } = [];

    /// <summary>Max root categories shown in the desktop header bar before it would overflow.</summary>
    public const int HeaderLimit = 6;

    /// <summary>Max categories shown as homepage circles (the layout is a 4-up grid).</summary>
    public const int CirclesLimit = 4;
}
