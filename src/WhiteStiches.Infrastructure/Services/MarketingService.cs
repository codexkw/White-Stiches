using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class MarketingService(WhiteStichesDbContext db) : IMarketingService
{
    public async Task<DiscountValidationResult> ValidateDiscountCodeAsync(string code, decimal cartSubtotal,
        int cartItemCount, Guid? userId = null, CancellationToken ct = default)
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

        var amount = discount.Type switch
        {
            DiscountType.Percentage => Math.Round(cartSubtotal * discount.Value / 100m, 3),
            DiscountType.FixedAmount => Math.Min(discount.Value, cartSubtotal),
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

    public async Task<DiscountCode?> GetDiscountCodeAsync(int id, CancellationToken ct = default) =>
        await db.DiscountCodes.FindAsync([id], ct);

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
}
