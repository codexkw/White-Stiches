using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>A sellable product. Bilingual content; variants carry price/stock per option combination.</summary>
public class Product : BaseEntity
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>URL handle, unique (e.g., "atlas-wool-jacket").</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Rich-text description (HTML).</summary>
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    /// <summary>PDP "Material &amp; Care" tab content (HTML).</summary>
    public string? MaterialCareEn { get; set; }
    public string? MaterialCareAr { get; set; }

    /// <summary>PDP "Size &amp; Fit" tab content (HTML).</summary>
    public string? SizeFitEn { get; set; }
    public string? SizeFitAr { get; set; }

    /// <summary>Product type for organization/smart collections (e.g., "Jacket").</summary>
    public string? Type { get; set; }
    public string? Vendor { get; set; }

    /// <summary>Comma-separated tags for filtering and smart collections.</summary>
    public string? Tags { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    /// <summary>Scheduled publish time; null = published immediately when Active.</summary>
    public DateTime? PublishAtUtc { get; set; }

    public bool IsFeatured { get; set; }

    public string? SeoTitleEn { get; set; }
    public string? SeoTitleAr { get; set; }
    public string? SeoDescriptionEn { get; set; }
    public string? SeoDescriptionAr { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductOption> Options { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<CollectionProduct> CollectionProducts { get; set; } = [];
}
