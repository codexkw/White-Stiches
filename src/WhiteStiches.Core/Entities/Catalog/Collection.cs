using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>A merchandised set of products — manual (curated) or smart (rule-driven).</summary>
public class Collection : BaseEntity
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? ImageUrl { get; set; }
    public string? BannerUrl { get; set; }

    public bool IsSmart { get; set; }

    /// <summary>JSON rule set for smart collections (tag/price/type/vendor conditions with AND/OR).</summary>
    public string? RulesJson { get; set; }

    public CollectionSortOrder SortOrder { get; set; } = CollectionSortOrder.Manual;
    public bool IsActive { get; set; } = true;

    public string? SeoTitleEn { get; set; }
    public string? SeoTitleAr { get; set; }
    public string? SeoDescriptionEn { get; set; }
    public string? SeoDescriptionAr { get; set; }

    public ICollection<CollectionProduct> CollectionProducts { get; set; } = [];
}
