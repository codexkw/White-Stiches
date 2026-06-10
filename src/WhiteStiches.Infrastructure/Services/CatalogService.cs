using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class CatalogService(WhiteStichesDbContext db) : ICatalogService
{
    public async Task<PagedResult<Product>> GetProductsAsync(ProductQuery query, CancellationToken ct = default)
    {
        var products = db.Products
            .AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants.Where(v => v.IsActive))
            .AsQueryable();

        if (query.Status is not null)
        {
            products = products.Where(p => p.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p =>
                p.TitleEn.Contains(term) || p.TitleAr.Contains(term) ||
                (p.Tags != null && p.Tags.Contains(term)) ||
                (p.Type != null && p.Type.Contains(term)));
        }

        if (query.CategoryId is not null)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            products = products.Where(p => p.Category != null && p.Category.Slug == query.CategorySlug);
        }

        if (!string.IsNullOrWhiteSpace(query.CollectionSlug))
        {
            products = products.Where(p => p.CollectionProducts.Any(cp => cp.Collection.Slug == query.CollectionSlug));
        }

        if (!string.IsNullOrWhiteSpace(query.Size))
        {
            products = products.Where(p => p.Variants.Any(v =>
                v.Option1 == query.Size || v.Option2 == query.Size || v.Option3 == query.Size));
        }

        if (!string.IsNullOrWhiteSpace(query.Color))
        {
            products = products.Where(p => p.Variants.Any(v =>
                v.Option1 == query.Color || v.Option2 == query.Color || v.Option3 == query.Color));
        }

        if (query.PriceMin is not null)
        {
            products = products.Where(p => p.Variants.Any(v => v.Price >= query.PriceMin));
        }

        if (query.PriceMax is not null)
        {
            products = products.Where(p => p.Variants.Any(v => v.Price <= query.PriceMax));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            products = products.Where(p => p.Tags != null && p.Tags.Contains(query.Tag));
        }

        if (query.InStockOnly)
        {
            products = products.Where(p => p.Variants.Any(v => v.StockQuantity > 0 || v.AllowOversell));
        }

        products = query.Sort switch
        {
            ProductSort.Newest => products.OrderByDescending(p => p.CreatedAtUtc),
            ProductSort.PriceLowToHigh => products.OrderBy(p => p.Variants.Min(v => (decimal?)v.Price) ?? 0),
            ProductSort.PriceHighToLow => products.OrderByDescending(p => p.Variants.Max(v => (decimal?)v.Price) ?? 0),
            ProductSort.Alphabetical => products.OrderBy(p => p.TitleEn),
            // Best-selling needs order aggregation; approximate with featured-then-newest until reporting lands.
            ProductSort.BestSelling => products.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAtUtc),
            _ => products.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAtUtc)
        };

        var total = await products.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Product> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<Product?> GetProductBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Products
            .AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Options.OrderBy(o => o.Position))
            .Include(p => p.Variants.Where(v => v.IsActive).OrderBy(v => v.Position))
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<Product?> GetProductByIdAsync(int id, CancellationToken ct = default) =>
        db.Products
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Options.OrderBy(o => o.Position))
            .Include(p => p.Variants.OrderBy(v => v.Position))
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetFeaturedProductsAsync(int count = 8, CancellationToken ct = default) =>
        await db.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants.Where(v => v.IsActive))
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Take(count)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetRelatedProductsAsync(int productId, int count = 4, CancellationToken ct = default)
    {
        var categoryId = await db.Products
            .Where(p => p.Id == productId)
            .Select(p => p.CategoryId)
            .FirstOrDefaultAsync(ct);

        return await db.Products
            .AsNoTracking()
            .Where(p => p.Id != productId && p.Status == ProductStatus.Active)
            .OrderByDescending(p => p.CategoryId == categoryId)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Category>> GetCategoryTreeAsync(CancellationToken ct = default) =>
        await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentId == null)
            .Include(c => c.Children.Where(ch => ch.IsActive).OrderBy(ch => ch.SortOrder))
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public Task<Category?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Categories
            .AsNoTracking()
            .Include(c => c.Children.Where(ch => ch.IsActive).OrderBy(ch => ch.SortOrder))
            .FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<IReadOnlyList<Collection>> GetCollectionsAsync(CancellationToken ct = default) =>
        await db.Collections
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.TitleEn)
            .ToListAsync(ct);

    public Task<Collection?> GetCollectionBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive, ct);

    public async Task<Product> CreateProductAsync(Product product, CancellationToken ct = default)
    {
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
    {
        db.Products.Update(product);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteProductAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return;
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Category> CreateCategoryAsync(Category category, CancellationToken ct = default)
    {
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return category;
    }

    public async Task UpdateCategoryAsync(Category category, CancellationToken ct = default)
    {
        db.Categories.Update(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FindAsync([id], ct);
        if (category is null) return;
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Collection> CreateCollectionAsync(Collection collection, CancellationToken ct = default)
    {
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);
        return collection;
    }

    public async Task UpdateCollectionAsync(Collection collection, CancellationToken ct = default)
    {
        db.Collections.Update(collection);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCollectionAsync(int id, CancellationToken ct = default)
    {
        var collection = await db.Collections.FindAsync([id], ct);
        if (collection is null) return;
        db.Collections.Remove(collection);
        await db.SaveChangesAsync(ct);
    }

    public async Task AdjustInventoryAsync(InventoryAdjustment adjustment, CancellationToken ct = default)
    {
        var variant = await db.ProductVariants.FindAsync([adjustment.ProductVariantId], ct)
            ?? throw new InvalidOperationException($"Variant {adjustment.ProductVariantId} not found.");

        variant.StockQuantity += adjustment.QuantityDelta;
        db.InventoryAdjustments.Add(adjustment);
        await db.SaveChangesAsync(ct);
    }
}
