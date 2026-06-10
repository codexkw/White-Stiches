using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

/// <summary>List screen for /discounts (AD-MKT-01).</summary>
public class DiscountListViewModel
{
    public PagedResult<DiscountCode> Discounts { get; init; } = new();
    public string? Search { get; init; }
    public bool ActiveOnly { get; init; }
}

/// <summary>Create/edit form for a discount code. Property names are the exact form field names.</summary>
public class DiscountEditViewModel
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public DiscountType Type { get; set; } = DiscountType.Percentage;

    /// <summary>Percentage (0–100) or fixed KWD amount depending on Type. Ignored for FreeShipping.</summary>
    public decimal Value { get; set; }

    public decimal? MinPurchaseAmount { get; set; }
    public int? MinQuantity { get; set; }

    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerCustomer { get; set; }

    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string? EligibilityJson { get; set; }

    /// <summary>Display only — never written back to the entity.</summary>
    public int TimesUsed { get; set; }

    public static DiscountEditViewModel From(DiscountCode d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Type = d.Type,
        Value = d.Value,
        MinPurchaseAmount = d.MinPurchaseAmount,
        MinQuantity = d.MinQuantity,
        UsageLimitTotal = d.UsageLimitTotal,
        UsageLimitPerCustomer = d.UsageLimitPerCustomer,
        StartsAtUtc = d.StartsAtUtc,
        EndsAtUtc = d.EndsAtUtc,
        IsActive = d.IsActive,
        EligibilityJson = d.EligibilityJson,
        TimesUsed = d.TimesUsed
    };

    /// <summary>Copies form values onto the entity. TimesUsed is intentionally left untouched.</summary>
    public void Apply(DiscountCode entity)
    {
        entity.Code = Code;
        entity.Type = Type;
        entity.Value = Value;
        entity.MinPurchaseAmount = MinPurchaseAmount;
        entity.MinQuantity = MinQuantity;
        entity.UsageLimitTotal = UsageLimitTotal;
        entity.UsageLimitPerCustomer = UsageLimitPerCustomer;
        entity.StartsAtUtc = StartsAtUtc;
        entity.EndsAtUtc = EndsAtUtc;
        entity.IsActive = IsActive;
        entity.EligibilityJson = string.IsNullOrWhiteSpace(EligibilityJson) ? null : EligibilityJson;
    }
}

/// <summary>List screen for /newsletter.</summary>
public class NewsletterListViewModel
{
    public PagedResult<NewsletterSubscriber> Subscribers { get; init; } = new();
}
