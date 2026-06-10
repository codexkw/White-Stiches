using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Discount codes and newsletter (AD-MKT-01, SF-HOM-05).</summary>
public interface IMarketingService
{
    /// <summary>Validates a code against schedule, limits, and minimums. Returns null with a reason when invalid.</summary>
    Task<DiscountValidationResult> ValidateDiscountCodeAsync(string code, decimal cartSubtotal, int cartItemCount, Guid? userId = null, CancellationToken ct = default);

    Task<IReadOnlyList<DiscountCode>> GetDiscountCodesAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<DiscountCode?> GetDiscountCodeAsync(int id, CancellationToken ct = default);
    Task<DiscountCode> SaveDiscountCodeAsync(DiscountCode code, CancellationToken ct = default);
    Task DeleteDiscountCodeAsync(int id, CancellationToken ct = default);

    Task<bool> SubscribeToNewsletterAsync(string email, bool whatsAppOptIn, string languageCode = "en", string? source = null, CancellationToken ct = default);
    Task<PagedResult<NewsletterSubscriber>> GetSubscribersAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
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
