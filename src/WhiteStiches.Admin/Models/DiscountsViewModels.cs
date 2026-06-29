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

    /// <summary>Display only — never written back to the entity.</summary>
    public int TimesUsed { get; set; }

    // ---- Eligibility picker (populated by the controller; not posted by the main form) ----

    /// <summary>Products this code is restricted to (empty = no product restriction).</summary>
    public IReadOnlyList<EligibilityProductRow> EligibleProducts { get; set; } = [];

    /// <summary>All collections, each flagged whether it is currently selected for this code.</summary>
    public IReadOnlyList<EligibilityCollectionRow> EligibleCollections { get; set; } = [];

    /// <summary>Current product-search term + its results (shown only after a search).</summary>
    public string? ProductSearch { get; set; }
    public bool ShowSearchResults { get; set; }
    public IReadOnlyList<EligibilityProductRow> SearchResults { get; set; } = [];

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
        TimesUsed = d.TimesUsed
    };

    /// <summary>
    /// Copies form values onto the entity. TimesUsed and EligibilityJson are intentionally left
    /// untouched — eligibility is managed by its own picker endpoints, not the main form.
    /// </summary>
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
    }
}

/// <summary>One product row in the discount eligibility picker (selected list or search results).</summary>
public record EligibilityProductRow(int ProductId, string Title, string Slug, string? ImageUrl, decimal? Price);

/// <summary>One collection in the eligibility picker, with whether it's currently selected for the code.</summary>
public record EligibilityCollectionRow(int Id, string Title, bool Selected);

/// <summary>List screen for /newsletter.</summary>
public class NewsletterListViewModel
{
    public PagedResult<NewsletterSubscriber> Subscribers { get; init; } = new();
}
