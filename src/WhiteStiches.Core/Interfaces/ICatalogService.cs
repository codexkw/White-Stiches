using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Catalog browsing and management — products, categories, collections, inventory.</summary>
public interface ICatalogService
{
    // ---- Storefront reads ----
    Task<PagedResult<Product>> GetProductsAsync(ProductQuery query, CancellationToken ct = default);
    Task<Product?> GetProductBySlugAsync(string slug, CancellationToken ct = default);
    Task<Product?> GetProductByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetFeaturedProductsAsync(int count = 8, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetRelatedProductsAsync(int productId, int count = 4, CancellationToken ct = default);

    Task<IReadOnlyList<Category>> GetCategoryTreeAsync(CancellationToken ct = default);
    Task<Category?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<Collection>> GetCollectionsAsync(CancellationToken ct = default);
    Task<Collection?> GetCollectionBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Distinct size/colour values available for filtering within the given category/collection/search
    /// scope. Deliberately ignores the size/colour/price/stock selections so picking one value never
    /// removes the others from the sidebar.
    /// </summary>
    Task<ProductFilterFacets> GetFilterFacetsAsync(ProductQuery scope, CancellationToken ct = default);

    // ---- Admin writes ----
    Task<Product> CreateProductAsync(Product product, CancellationToken ct = default);
    Task UpdateProductAsync(Product product, CancellationToken ct = default);
    Task DeleteProductAsync(int id, CancellationToken ct = default);

    Task<Category> CreateCategoryAsync(Category category, CancellationToken ct = default);
    Task UpdateCategoryAsync(Category category, CancellationToken ct = default);
    Task DeleteCategoryAsync(int id, CancellationToken ct = default);

    Task<Collection> CreateCollectionAsync(Collection collection, CancellationToken ct = default);
    Task UpdateCollectionAsync(Collection collection, CancellationToken ct = default);
    Task DeleteCollectionAsync(int id, CancellationToken ct = default);

    /// <summary>Adjusts variant stock and records an immutable InventoryAdjustment row.</summary>
    Task AdjustInventoryAsync(InventoryAdjustment adjustment, CancellationToken ct = default);

    // ---- Admin reads (AD-PRD-01..04) — include Draft/Archived; storefront reads stay Active-only ----

    /// <summary>Paged admin list across all statuses with Images, Variants (incl. inactive), and Category.</summary>
    Task<PagedResult<Product>> GetProductsAdminAsync(string? search = null, ProductStatus? status = null,
        int? categoryId = null, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>Full no-tracking edit graph: Images, Options, and Variants ordered, plus Category.</summary>
    Task<Product?> GetProductForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Most recent stock movements across all variants of a product, newest first.</summary>
    Task<IReadOnlyList<InventoryAdjustment>> GetInventoryHistoryAsync(int productId, int take = 50, CancellationToken ct = default);

    /// <summary>All categories regardless of IsActive, as a flat list (admin builds the tree).</summary>
    Task<IReadOnlyList<Category>> GetCategoriesAdminAsync(CancellationToken ct = default);

    /// <summary>Product count per CategoryId (categories with no products are absent).</summary>
    Task<IReadOnlyDictionary<int, int>> GetCategoryProductCountsAsync(CancellationToken ct = default);

    // ---- Admin writes: options / variants / images ----

    /// <summary>
    /// Replaces the product's option axes (up to 3) and regenerates the variant matrix:
    /// variants whose option combination still exists keep their price/stock/SKU; new
    /// combinations are created (price defaults to the current max variant price);
    /// combinations that disappeared are deleted. No options collapses to one default variant.
    /// </summary>
    Task SetProductOptionsAsync(int productId, IReadOnlyList<ProductOptionInput> options, CancellationToken ct = default);

    /// <summary>Bulk-saves the spreadsheet variant editor rows; rows for other products are ignored.</summary>
    Task UpdateVariantsAsync(int productId, IReadOnlyList<VariantUpdateRow> rows, CancellationToken ct = default);

    /// <summary>Appends an image (next SortOrder) with the stored /media URL.</summary>
    Task<ProductImage> AddProductImageAsync(int productId, string url, CancellationToken ct = default);

    /// <summary>Deletes an image, clears variant references to it, closes the SortOrder gap. Returns its URL, or null when not found.</summary>
    Task<string?> DeleteProductImageAsync(int productId, int imageId, CancellationToken ct = default);

    /// <summary>Swaps SortOrder with the previous (up) or next (down) image; no-op at the edges.</summary>
    Task MoveProductImageAsync(int productId, int imageId, bool moveUp, CancellationToken ct = default);

    /// <summary>Saves bilingual alt text and the gallery colour binding for one image.</summary>
    Task UpdateProductImageMetaAsync(int productId, int imageId, string? altEn, string? altAr, string? colorName, CancellationToken ct = default);

    // ---- Admin slug helpers ----

    /// <summary>Returns baseSlug, or baseSlug-2/-3/... until unique among products (excluding one id when editing).</summary>
    Task<string> EnsureUniqueProductSlugAsync(string baseSlug, int? excludeProductId = null, CancellationToken ct = default);

    /// <summary>Returns baseSlug, or baseSlug-2/-3/... until unique among categories (excluding one id when editing).</summary>
    Task<string> EnsureUniqueCategorySlugAsync(string baseSlug, int? excludeCategoryId = null, CancellationToken ct = default);
}
