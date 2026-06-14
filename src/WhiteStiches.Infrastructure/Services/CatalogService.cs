using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;
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

    public async Task<ProductFilterFacets> GetFilterFacetsAsync(ProductQuery scope, CancellationToken ct = default)
    {
        // Scope facets to the current category/collection/search context only — NOT the size/colour/
        // price/stock selections — so choosing one value never hides the others. Sizes are variant
        // Option1, colours Option2: the convention the catalog (and seeder) are built on.
        var products = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(scope.CategorySlug))
            products = products.Where(p => p.Category != null && p.Category.Slug == scope.CategorySlug);

        if (!string.IsNullOrWhiteSpace(scope.CollectionSlug))
            products = products.Where(p => p.CollectionProducts.Any(cp => cp.Collection.Slug == scope.CollectionSlug));

        if (!string.IsNullOrWhiteSpace(scope.Search))
        {
            var term = scope.Search.Trim();
            products = products.Where(p =>
                p.TitleEn.Contains(term) || p.TitleAr.Contains(term) ||
                (p.Tags != null && p.Tags.Contains(term)) ||
                (p.Type != null && p.Type.Contains(term)));
        }

        var variants = products.SelectMany(p => p.Variants.Where(v => v.IsActive));

        var sizes = await variants
            .Where(v => v.Option1 != null && v.Option1 != "")
            .Select(v => v.Option1!)
            .Distinct()
            .ToListAsync(ct);

        var colors = await variants
            .Where(v => v.Option2 != null && v.Option2 != "")
            .Select(v => v.Option2!)
            .Distinct()
            .ToListAsync(ct);

        return new ProductFilterFacets
        {
            Sizes = OrderSizes(sizes),
            Colors = colors.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    // Canonical apparel size order; values not in the list sort last, alphabetically.
    private static readonly string[] SizeRank =
        ["XXS", "XS", "S", "M", "L", "XL", "XXL", "XXXL", "2XL", "3XL", "4XL"];

    private static List<string> OrderSizes(IEnumerable<string> sizes) =>
        sizes
            .OrderBy(s =>
            {
                var i = Array.FindIndex(SizeRank, r => string.Equals(r, s, StringComparison.OrdinalIgnoreCase));
                return i < 0 ? int.MaxValue : i;
            })
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        // Variant→Image FK has no DB referential action (ClientSetNull); clear it first
        // so the product's image cascade can never be blocked.
        var withImage = await db.ProductVariants
            .Where(v => v.ProductId == id && v.ImageId != null)
            .ToListAsync(ct);
        foreach (var variant in withImage)
        {
            variant.ImageId = null;
        }

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

    // ------------------------------------------------------------------ admin reads

    public async Task<PagedResult<Product>> GetProductsAdminAsync(string? search = null, ProductStatus? status = null,
        int? categoryId = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var products = db.Products
            .AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants)
            .Include(p => p.Category)
            .AsQueryable();

        if (status is not null)
        {
            products = products.Where(p => p.Status == status);
        }

        if (categoryId is not null)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            products = products.Where(p =>
                p.TitleEn.Contains(term) || p.TitleAr.Contains(term) || p.Slug.Contains(term));
        }

        products = products
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id);

        var total = await products.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Product> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<Product?> GetProductForEditAsync(int id, CancellationToken ct = default) =>
        db.Products
            .AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Options.OrderBy(o => o.Position))
            .Include(p => p.Variants.OrderBy(v => v.Position))
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<InventoryAdjustment>> GetInventoryHistoryAsync(int productId, int take = 50,
        CancellationToken ct = default) =>
        await db.InventoryAdjustments
            .AsNoTracking()
            .Include(a => a.ProductVariant)
            .Where(a => a.ProductVariant.ProductId == productId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Category>> GetCategoriesAdminAsync(CancellationToken ct = default) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.NameEn)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, int>> GetCategoryProductCountsAsync(CancellationToken ct = default) =>
        await db.Products
            .Where(p => p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    // ------------------------------------------------------------------ admin writes

    public async Task SetProductOptionsAsync(int productId, IReadOnlyList<ProductOptionInput> options,
        CancellationToken ct = default)
    {
        var product = await db.Products
            .Include(p => p.Options)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var clean = options
            .Select(o => new
            {
                Name = (o.Name ?? string.Empty).Trim(),
                Values = (o.ValuesCsv ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .Where(o => o.Name.Length > 0 && o.Values.Length > 0)
            .Take(3)
            .ToList();

        // Replace the option axes (Position maps to Variant.Option1..3).
        db.ProductOptions.RemoveRange(product.Options.ToList());
        for (var i = 0; i < clean.Count; i++)
        {
            db.ProductOptions.Add(new ProductOption
            {
                ProductId = productId,
                NameEn = clean[i].Name,
                NameAr = clean[i].Name,
                Position = i + 1,
                ValuesCsv = string.Join(",", clean[i].Values)
            });
        }

        // Cartesian product of the option values; zero options = one (null,null,null) combo.
        var combos = new List<string?[]> { new string?[3] };
        for (var pos = 0; pos < clean.Count; pos++)
        {
            combos = combos
                .SelectMany(combo => clean[pos].Values.Select(value =>
                {
                    var next = (string?[])combo.Clone();
                    next[pos] = value;
                    return next;
                }))
                .ToList();
        }

        static string Key(string? a, string? b, string? c) => $"{a}\u001f{b}\u001f{c}";

        var existingByCombo = product.Variants
            .GroupBy(v => Key(v.Option1, v.Option2, v.Option3), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var defaultPrice = product.Variants.Count > 0 ? product.Variants.Max(v => v.Price) : 0m;
        var kept = new HashSet<int>();
        var position = 1;

        foreach (var combo in combos)
        {
            if (existingByCombo.TryGetValue(Key(combo[0], combo[1], combo[2]), out var existing))
            {
                existing.Option1 = combo[0];
                existing.Option2 = combo[1];
                existing.Option3 = combo[2];
                existing.Position = position++;
                kept.Add(existing.Id);
            }
            else
            {
                db.ProductVariants.Add(new ProductVariant
                {
                    ProductId = productId,
                    Option1 = combo[0],
                    Option2 = combo[1],
                    Option3 = combo[2],
                    Price = defaultPrice,
                    StockQuantity = 0,
                    IsActive = true,
                    Position = position++
                });
            }
        }

        // Combinations that disappeared: safe to delete — order lines are snapshots (no FK),
        // and cart items / stock history cascade with the variant.
        var removed = product.Variants
            .Where(v => v.Id != 0 && !kept.Contains(v.Id))
            .ToList();
        db.ProductVariants.RemoveRange(removed);

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateVariantsAsync(int productId, IReadOnlyList<VariantUpdateRow> rows,
        CancellationToken ct = default)
    {
        var variants = await db.ProductVariants
            .Where(v => v.ProductId == productId)
            .ToListAsync(ct);
        var byId = variants.ToDictionary(v => v.Id);

        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.Id, out var variant)) continue;

            variant.Price = row.Price < 0 ? 0 : row.Price;
            variant.CompareAtPrice = row.CompareAtPrice is < 0 ? null : row.CompareAtPrice;
            variant.Sku = string.IsNullOrWhiteSpace(row.Sku) ? null : row.Sku.Trim();
            variant.StockQuantity = row.StockQuantity;
            variant.LowStockThreshold = row.LowStockThreshold < 0 ? 0 : row.LowStockThreshold;
            variant.AllowOversell = row.AllowOversell;
            variant.IsActive = row.IsActive;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductImage> AddProductImageAsync(int productId, string url, MediaKind kind = MediaKind.Image, CancellationToken ct = default)
    {
        var maxSort = await db.ProductImages
            .Where(i => i.ProductId == productId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct) ?? -1;

        var image = new ProductImage { ProductId = productId, Url = url, MediaKind = kind, SortOrder = maxSort + 1 };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync(ct);
        return image;
    }

    public async Task<string?> DeleteProductImageAsync(int productId, int imageId, CancellationToken ct = default)
    {
        var image = await db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);
        if (image is null) return null;

        // Variant→Image FK has no DB referential action; clear references in the same save.
        var referencing = await db.ProductVariants
            .Where(v => v.ImageId == imageId)
            .ToListAsync(ct);
        foreach (var variant in referencing)
        {
            variant.ImageId = null;
        }

        var later = await db.ProductImages
            .Where(i => i.ProductId == productId && i.Id != imageId && i.SortOrder > image.SortOrder)
            .ToListAsync(ct);
        foreach (var sibling in later)
        {
            sibling.SortOrder--;
        }

        db.ProductImages.Remove(image);
        await db.SaveChangesAsync(ct);
        return image.Url;
    }

    public async Task MoveProductImageAsync(int productId, int imageId, bool moveUp, CancellationToken ct = default)
    {
        var images = await db.ProductImages
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);

        var index = images.FindIndex(i => i.Id == imageId);
        if (index < 0) return;

        var target = moveUp ? index - 1 : index + 1;
        if (target < 0 || target >= images.Count) return;

        (images[index].SortOrder, images[target].SortOrder) = (images[target].SortOrder, images[index].SortOrder);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateProductImageMetaAsync(int productId, int imageId, string? altEn, string? altAr,
        string? colorName, CancellationToken ct = default)
    {
        var image = await db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);
        if (image is null) return;

        image.AltEn = string.IsNullOrWhiteSpace(altEn) ? null : altEn.Trim();
        image.AltAr = string.IsNullOrWhiteSpace(altAr) ? null : altAr.Trim();
        image.ColorName = string.IsNullOrWhiteSpace(colorName) ? null : colorName.Trim();
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ admin slug helpers

    public async Task<string> EnsureUniqueProductSlugAsync(string baseSlug, int? excludeProductId = null,
        CancellationToken ct = default)
    {
        var slug = string.IsNullOrWhiteSpace(baseSlug) ? "product" : baseSlug.Trim();
        var candidate = slug;
        var suffix = 2;

        while (await db.Products.AnyAsync(
                   p => p.Slug == candidate && (excludeProductId == null || p.Id != excludeProductId), ct))
        {
            candidate = $"{slug}-{suffix++}";
        }

        return candidate;
    }

    public async Task<string> EnsureUniqueCategorySlugAsync(string baseSlug, int? excludeCategoryId = null,
        CancellationToken ct = default)
    {
        var slug = string.IsNullOrWhiteSpace(baseSlug) ? "category" : baseSlug.Trim();
        var candidate = slug;
        var suffix = 2;

        while (await db.Categories.AnyAsync(
                   c => c.Slug == candidate && (excludeCategoryId == null || c.Id != excludeCategoryId), ct))
        {
            candidate = $"{slug}-{suffix++}";
        }

        return candidate;
    }
}
