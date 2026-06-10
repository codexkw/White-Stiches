namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>Hierarchical product category (e.g., Women &gt; Dresses &gt; Maxi). Feeds mega-menu and breadcrumbs.</summary>
public class Category : BaseEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];
}
