using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Discount codes and newsletter (AD-MKT-01, SF-HOM-05).</summary>
public interface IMarketingService
{
    /// <summary>Validates a code against schedule, limits, and minimums. Returns null with a reason when invalid.</summary>
    Task<DiscountValidationResult> ValidateDiscountCodeAsync(string code, decimal cartSubtotal, int cartItemCount, Guid? userId = null, CancellationToken ct = default);

    Task<IReadOnlyList<DiscountCode>> GetDiscountCodesAsync(bool activeOnly = false, CancellationToken ct = default);

    /// <summary>Paged discount-code listing for the back office with code search and active-only filter (AD-MKT-01).</summary>
    Task<PagedResult<DiscountCode>> GetDiscountCodesPagedAsync(string? search = null, bool activeOnly = false,
        int page = 1, int pageSize = 20, CancellationToken ct = default);

    Task<DiscountCode?> GetDiscountCodeAsync(int id, CancellationToken ct = default);
    Task<DiscountCode> SaveDiscountCodeAsync(DiscountCode code, CancellationToken ct = default);
    Task DeleteDiscountCodeAsync(int id, CancellationToken ct = default);

    /// <summary>True when a different discount code (excluding <paramref name="excludeId"/>) already uses this code (case-insensitive).</summary>
    Task<bool> DiscountCodeExistsAsync(string code, int excludeId = 0, CancellationToken ct = default);

    /// <summary>True when at least one order references the discount code — delete should deactivate instead.</summary>
    Task<bool> IsDiscountCodeUsedByOrdersAsync(int discountCodeId, CancellationToken ct = default);

    Task<bool> SubscribeToNewsletterAsync(string email, bool whatsAppOptIn, string languageCode = "en", string? source = null, CancellationToken ct = default);
    Task<PagedResult<NewsletterSubscriber>> GetSubscribersAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>All active (non-unsubscribed) subscribers, newest first — used by the admin CSV export.</summary>
    Task<IReadOnlyList<NewsletterSubscriber>> GetAllSubscribersAsync(CancellationToken ct = default);
}

public class DiscountValidationResult
{
    public bool IsValid { get; init; }
    public DiscountCode? Code { get; init; }
    public decimal DiscountAmount { get; init; }

    /// <summary>Failure reason key for AR/EN display: "not_found", "expired", "not_started", "usage_limit", "min_purchase", "min_quantity", "inactive".</summary>
    public string? FailureReason { get; init; }

    public static DiscountValidationResult Valid(DiscountCode code, decimal amount) =>
        new() { IsValid = true, Code = code, DiscountAmount = amount };

    public static DiscountValidationResult Invalid(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}
