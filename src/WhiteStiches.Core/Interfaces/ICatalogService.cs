using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Models;

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
}
