using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Marketing;

/// <summary>Discount code with usage limits and schedule (AD-MKT-01).</summary>
public class DiscountCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public DiscountType Type { get; set; } = DiscountType.Percentage;

    /// <summary>Percentage (0–100) or fixed KWD amount depending on Type. Ignored for FreeShipping.</summary>
    public decimal Value { get; set; }

    public decimal? MinPurchaseAmount { get; set; }
    public int? MinQuantity { get; set; }

    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int TimesUsed { get; set; }

    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optional eligibility scope (customer/segment/product/collection rules) as JSON.</summary>
    public string? EligibilityJson { get; set; }
}
