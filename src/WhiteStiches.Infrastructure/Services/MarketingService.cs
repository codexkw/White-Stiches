using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class MarketingService(WhiteStichesDbContext db) : IMarketingService
{
    public async Task<DiscountValidationResult> ValidateDiscountCodeAsync(string code, decimal cartSubtotal,
        int cartItemCount, IReadOnlyList<DiscountLineItem>? lines = null, Guid? userId = null, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var discount = await db.DiscountCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == normalized, ct);

        if (discount is null) return DiscountValidationResult.Invalid("not_found");
        if (!discount.IsActive) return DiscountValidationResult.Invalid("inactive");

        var now = DateTime.UtcNow;
        if (discount.StartsAtUtc is not null && discount.StartsAtUtc > now)
            return DiscountValidationResult.Invalid("not_started");
        if (discount.EndsAtUtc is not null && discount.EndsAtUtc < now)
            return DiscountValidationResult.Invalid("expired");

        if (discount.UsageLimitTotal is not null && discount.TimesUsed >= discount.UsageLimitTotal)
            return DiscountValidationResult.Invalid("usage_limit");

        if (discount.UsageLimitPerCustomer is not null && userId is not null)
        {
            var customerUses = await db.Orders.CountAsync(o => o.UserId == userId && o.DiscountCodeId == discount.Id, ct);
            if (customerUses >= discount.UsageLimitPerCustomer)
                return DiscountValidationResult.Invalid("usage_limit");
        }

        if (discount.MinPurchaseAmount is not null && cartSubtotal < discount.MinPurchaseAmount)
            return DiscountValidationResult.Invalid("min_purchase");

        if (discount.MinQuantity is not null && cartItemCount < discount.MinQuantity)
            return DiscountValidationResult.Invalid("min_quantity");

        // The base the discount applies to. With product/collection eligibility, only the eligible
        // lines count; min-purchase/min-quantity above still gate on the whole cart.
        var discountBase = cartSubtotal;
        var eligibility = DiscountEligibility.Parse(discount.EligibilityJson);
        if (eligibility.HasRestrictions && lines is not null)
        {
            var eligibleProductIds = new HashSet<int>(eligibility.Products);
            if (eligibility.Collections.Count > 0)
            {
                var fromCollections = await db.CollectionProducts
                    .Where(cp => eligibility.Collections.Contains(cp.CollectionId))
                    .Select(cp => cp.ProductId)
                    .ToListAsync(ct);
                foreach (var pid in fromCollections) eligibleProductIds.Add(pid);
            }

            discountBase = lines.Where(l => eligibleProductIds.Contains(l.ProductId)).Sum(l => l.LineTotal);
            if (discountBase <= 0m)
                return DiscountValidationResult.Invalid("not_eligible");
        }

        var amount = discount.Type switch
        {
            DiscountType.Percentage => Math.Round(discountBase * discount.Value / 100m, 3),
            DiscountType.FixedAmount => Math.Min(discount.Value, discountBase),
            DiscountType.FreeShipping => 0m,
            _ => 0m
        };

        return DiscountValidationResult.Valid(discount, amount);
    }

    public async Task<IReadOnlyList<DiscountCode>> GetDiscountCodesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = db.DiscountCodes.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(d => d.IsActive);
        return await query.OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct);
    }

    public async Task<PagedResult<DiscountCode>> GetDiscountCodesPagedAsync(string? search = null, bool activeOnly = false,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.DiscountCodes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(d => d.Code.ToUpper().Contains(term));
        }

        if (activeOnly) query = query.Where(d => d.IsActive);

        query = query.OrderByDescending(d => d.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<DiscountCode> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<DiscountCode?> GetDiscountCodeAsync(int id, CancellationToken ct = default) =>
        await db.DiscountCodes.FindAsync([id], ct);

    public async Task<bool> DiscountCodeExistsAsync(string code, int excludeId = 0, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await db.DiscountCodes.AsNoTracking()
            .AnyAsync(d => d.Id != excludeId && d.Code.ToUpper() == normalized, ct);
    }

    public Task<bool> IsDiscountCodeUsedByOrdersAsync(int discountCodeId, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().AnyAsync(o => o.DiscountCodeId == discountCodeId, ct);

    public async Task<DiscountCode> SaveDiscountCodeAsync(DiscountCode code, CancellationToken ct = default)
    {
        code.Code = code.Code.Trim().ToUpperInvariant();

        if (code.Id == 0)
        {
            db.DiscountCodes.Add(code);
        }
        else
        {
            db.DiscountCodes.Update(code);
        }

        await db.SaveChangesAsync(ct);
        return code;
    }

    public async Task DeleteDiscountCodeAsync(int id, CancellationToken ct = default)
    {
        var code = await db.DiscountCodes.FindAsync([id], ct);
        if (code is null) return;

        db.DiscountCodes.Remove(code);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- eligibility

    public async Task<DiscountEligibility> GetEligibilityAsync(int discountId, CancellationToken ct = default)
    {
        var json = await db.DiscountCodes.AsNoTracking()
            .Where(d => d.Id == discountId)
            .Select(d => d.EligibilityJson)
            .FirstOrDefaultAsync(ct);
        return DiscountEligibility.Parse(json);
    }

    public async Task<bool> AddEligibleProductAsync(int discountId, int productId, CancellationToken ct = default)
    {
        var discount = await db.DiscountCodes.FindAsync([discountId], ct);
        if (discount is null) return false;
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct)) return false;

        var eligibility = DiscountEligibility.Parse(discount.EligibilityJson);
        if (eligibility.Products.Contains(productId)) return false;

        eligibility.Products.Add(productId);
        discount.EligibilityJson = eligibility.ToJsonOrNull();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveEligibleProductAsync(int discountId, int productId, CancellationToken ct = default)
    {
        var discount = await db.DiscountCodes.FindAsync([discountId], ct);
        if (discount is null) return false;

        var eligibility = DiscountEligibility.Parse(discount.EligibilityJson);
        if (!eligibility.Products.Remove(productId)) return false;

        discount.EligibilityJson = eligibility.ToJsonOrNull();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetEligibleCollectionsAsync(int discountId, IReadOnlyList<int> collectionIds, CancellationToken ct = default)
    {
        var discount = await db.DiscountCodes.FindAsync([discountId], ct);
        if (discount is null) return;

        var valid = await db.Collections
            .Where(c => collectionIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var eligibility = DiscountEligibility.Parse(discount.EligibilityJson);
        eligibility.Collections = valid.Distinct().ToList();
        discount.EligibilityJson = eligibility.ToJsonOrNull();
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Product>> SearchProductsAsync(string? term, int take = 20, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants)
            .Where(p => p.Status == ProductStatus.Active);

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

    public async Task<IReadOnlyList<Product>> GetProductsByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        return await db.Products.AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Collection>> GetAllCollectionsAsync(CancellationToken ct = default) =>
        await db.Collections.AsNoTracking()
            .OrderBy(c => c.TitleEn)
            .ToListAsync(ct);

    public async Task<bool> SubscribeToNewsletterAsync(string email, bool whatsAppOptIn,
        string languageCode = "en", string? source = null, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == normalized, ct);

        if (existing is not null)
        {
            // Re-subscribe if previously unsubscribed
            existing.UnsubscribedAtUtc = null;
            existing.WhatsAppOptIn = existing.WhatsAppOptIn || whatsAppOptIn;
            await db.SaveChangesAsync(ct);
            return false;
        }

        db.NewsletterSubscribers.Add(new NewsletterSubscriber
        {
            Email = normalized,
            WhatsAppOptIn = whatsAppOptIn,
            LanguageCode = languageCode,
            Source = source
        });

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<NewsletterSubscriber>> GetSubscribersAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = db.NewsletterSubscribers
            .AsNoTracking()
            .Where(s => s.UnsubscribedAtUtc == null)
            .OrderByDescending(s => s.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<NewsletterSubscriber> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<NewsletterSubscriber>> GetAllSubscribersAsync(CancellationToken ct = default) =>
        await db.NewsletterSubscribers.AsNoTracking()
            .Where(s => s.UnsubscribedAtUtc == null)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
}
