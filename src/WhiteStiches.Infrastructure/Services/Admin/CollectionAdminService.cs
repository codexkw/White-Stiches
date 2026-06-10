using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services.Admin;

public class CollectionAdminService(WhiteStichesDbContext db) : ICollectionAdminService
{
    public async Task<PagedResult<CollectionListItem>> GetListAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = db.Collections.AsNoTracking()
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .ThenByDescending(c => c.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CollectionListItem(
                c.Id,
                c.TitleEn,
                c.Slug,
                c.IsSmart,
                c.IsActive,
                c.CollectionProducts.Count,
                c.UpdatedAtUtc ?? c.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CollectionListItem> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<Collection?> GetForEditAsync(int id, CancellationToken ct = default) =>
        db.Collections.AsNoTracking()
            .Include(c => c.CollectionProducts.OrderBy(cp => cp.Position))
                .ThenInclude(cp => cp.Product)
                .ThenInclude(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(c => c.CollectionProducts)
                .ThenInclude(cp => cp.Product)
                .ThenInclude(p => p.Variants)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Collection> SaveAsync(Collection collection, CancellationToken ct = default)
    {
        collection.Slug = await EnsureUniqueSlugAsync(collection, ct);

        if (collection.Id == 0)
        {
            db.Collections.Add(collection);
        }
        else
        {
            var existing = await db.Collections.FirstAsync(c => c.Id == collection.Id, ct);
            collection.CreatedAtUtc = existing.CreatedAtUtc;
            collection.UpdatedAtUtc = DateTime.UtcNow;
            db.Entry(existing).CurrentValues.SetValues(collection);
        }

        await db.SaveChangesAsync(ct);
        return collection;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var collection = await db.Collections
            .Include(c => c.CollectionProducts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null) return false;

        db.CollectionProducts.RemoveRange(collection.CollectionProducts);
        db.Collections.Remove(collection);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Product>> SearchProductsForPickerAsync(int collectionId, string? term,
        int take = 20, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants)
            .Where(p => p.Status == ProductStatus.Active)
            .Where(p => !p.CollectionProducts.Any(cp => cp.CollectionId == collectionId));

        var t = term?.Trim();
        if (!string.IsNullOrEmpty(t))
        {
            query = query.Where(p =>
                p.TitleEn.Contains(t) ||
                p.TitleAr.Contains(t) ||
                p.Slug.Contains(t) ||
                (p.Type != null && p.Type.Contains(t)) ||
                (p.Vendor != null && p.Vendor.Contains(t)) ||
                (p.Tags != null && p.Tags.Contains(t)));
        }

        return await query.OrderBy(p => p.TitleEn).Take(take).ToListAsync(ct);
    }

    public async Task<bool> AddProductAsync(int collectionId, int productId, CancellationToken ct = default)
    {
        var collectionExists = await db.Collections.AnyAsync(c => c.Id == collectionId, ct);
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, ct);
        if (!collectionExists || !productExists) return false;

        var alreadyIn = await db.CollectionProducts
            .AnyAsync(cp => cp.CollectionId == collectionId && cp.ProductId == productId, ct);
        if (alreadyIn) return false;

        var maxPosition = await db.CollectionProducts
            .Where(cp => cp.CollectionId == collectionId)
            .MaxAsync(cp => (int?)cp.Position, ct) ?? -1;

        db.CollectionProducts.Add(new CollectionProduct
        {
            CollectionId = collectionId,
            ProductId = productId,
            Position = maxPosition + 1
        });
        await TouchCollectionAsync(collectionId, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveProductAsync(int collectionId, int productId, CancellationToken ct = default)
    {
        var row = await db.CollectionProducts
            .FirstOrDefaultAsync(cp => cp.CollectionId == collectionId && cp.ProductId == productId, ct);
        if (row is null) return false;

        db.CollectionProducts.Remove(row);

        // Re-sequence the remaining rows so positions stay dense.
        var remaining = await db.CollectionProducts
            .Where(cp => cp.CollectionId == collectionId && cp.ProductId != productId)
            .OrderBy(cp => cp.Position).ThenBy(cp => cp.ProductId)
            .ToListAsync(ct);
        for (var i = 0; i < remaining.Count; i++) remaining[i].Position = i;

        await TouchCollectionAsync(collectionId, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MoveProductAsync(int collectionId, int productId, bool moveUp, CancellationToken ct = default)
    {
        var rows = await db.CollectionProducts
            .Where(cp => cp.CollectionId == collectionId)
            .OrderBy(cp => cp.Position).ThenBy(cp => cp.ProductId)
            .ToListAsync(ct);

        var index = rows.FindIndex(cp => cp.ProductId == productId);
        if (index < 0) return false;

        var target = moveUp ? index - 1 : index + 1;
        if (target < 0 || target >= rows.Count) return false;

        (rows[index], rows[target]) = (rows[target], rows[index]);
        for (var i = 0; i < rows.Count; i++) rows[i].Position = i;

        await TouchCollectionAsync(collectionId, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SaveRulesAsync(int collectionId, CollectionRuleSet ruleSet, CancellationToken ct = default)
    {
        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == collectionId, ct);
        if (collection is null) return false;

        collection.RulesJson = ruleSet.ToJson();
        collection.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Product>> EvaluateRulesAsync(int collectionId, CancellationToken ct = default)
    {
        var rulesJson = await db.Collections.AsNoTracking()
            .Where(c => c.Id == collectionId)
            .Select(c => c.RulesJson)
            .FirstOrDefaultAsync(ct);

        var ruleSet = CollectionRuleSet.Parse(rulesJson);
        if (ruleSet is null || ruleSet.Rules.Count == 0) return [];

        var rules = ruleSet.Rules
            .Where(r => CollectionRuleSet.AllowedFields.Contains(r.Field, StringComparer.OrdinalIgnoreCase)
                        && CollectionRuleSet.AllowedOperators.Contains(r.Operator, StringComparer.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(r.Value))
            .ToList();
        if (rules.Count == 0) return [];

        var candidates = await db.Products.AsNoTracking()
            .Include(p => p.Variants)
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync(ct);

        var matchAny = string.Equals(ruleSet.Match, CollectionRuleSet.MatchAny, StringComparison.OrdinalIgnoreCase);

        return candidates
            .Where(p => matchAny ? rules.Any(r => MatchesRule(p, r)) : rules.All(r => MatchesRule(p, r)))
            .OrderBy(p => p.TitleEn, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> ApplyRulesAsync(int collectionId, CancellationToken ct = default)
    {
        var matched = await EvaluateRulesAsync(collectionId, ct);
        var matchedIds = matched.Select(p => p.Id).ToHashSet();

        var existing = await db.CollectionProducts
            .Where(cp => cp.CollectionId == collectionId)
            .ToListAsync(ct);

        db.CollectionProducts.RemoveRange(existing.Where(cp => !matchedIds.Contains(cp.ProductId)));

        var byProductId = existing.ToDictionary(cp => cp.ProductId);
        for (var i = 0; i < matched.Count; i++)
        {
            if (byProductId.TryGetValue(matched[i].Id, out var row))
            {
                row.Position = i;
            }
            else
            {
                db.CollectionProducts.Add(new CollectionProduct
                {
                    CollectionId = collectionId,
                    ProductId = matched[i].Id,
                    Position = i
                });
            }
        }

        await TouchCollectionAsync(collectionId, ct);
        await db.SaveChangesAsync(ct);
        return matched.Count;
    }

    // ------------------------------------------------------------- helpers

    private async Task<string> EnsureUniqueSlugAsync(Collection collection, CancellationToken ct)
    {
        var baseSlug = WhiteStiches.Core.Utils.Slug.Generate(
            string.IsNullOrWhiteSpace(collection.Slug) ? collection.TitleEn : collection.Slug);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "collection";

        var slug = baseSlug;
        var suffix = 2;
        while (await db.Collections.AnyAsync(c => c.Slug == slug && c.Id != collection.Id, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private async Task TouchCollectionAsync(int collectionId, CancellationToken ct)
    {
        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == collectionId, ct);
        if (collection is not null) collection.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool MatchesRule(Product product, CollectionRule rule)
    {
        var value = rule.Value.Trim();
        var op = rule.Operator.ToLowerInvariant();

        switch (rule.Field.ToLowerInvariant())
        {
            case "tag":
            {
                var tags = (product.Tags ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return op switch
                {
                    "equals" => tags.Any(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase)),
                    "contains" => tags.Any(t => t.Contains(value, StringComparison.OrdinalIgnoreCase)),
                    _ => false
                };
            }
            case "type":
                return TextMatches(product.Type, op, value);
            case "vendor":
                return TextMatches(product.Vendor, op, value);
            case "price":
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    return false;
                }

                var prices = product.Variants.Where(v => v.IsActive).Select(v => v.Price).ToList();
                if (prices.Count == 0) return false;

                return op switch
                {
                    "equals" => prices.Any(p => p == amount),
                    "gt" => prices.Any(p => p > amount),
                    "lt" => prices.Any(p => p < amount),
                    _ => false
                };
            }
            default:
                return false;
        }
    }

    private static bool TextMatches(string? actual, string op, string value)
    {
        if (string.IsNullOrEmpty(actual)) return false;

        return op switch
        {
            "equals" => string.Equals(actual, value, StringComparison.OrdinalIgnoreCase),
            "contains" => actual.Contains(value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
