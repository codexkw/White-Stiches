using WhiteStiches.Core.Entities.Orders;

namespace WhiteStiches.Core.Interfaces;

/// <summary>
/// Transactional email (Phase 1C-3). Implementations send branded, bilingual (EN/AR) mail and
/// must NEVER throw to the caller: a mail failure cannot be allowed to break checkout, fulfilment,
/// or the forgot-password flow. Every method logs and swallows its own errors.
/// </summary>
public interface IEmailService
{
    /// <summary>Password-reset link email. <paramref name="languageCode"/> is "en"/"ar".</summary>
    Task SendPasswordResetAsync(string toEmail, string? toName, string? languageCode, string resetLink,
        CancellationToken ct = default);

    /// <summary>Order confirmation — sent once payment is captured (or a manual order is placed).</summary>
    Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default);

    /// <summary>Shipment notification — sent when an order is fulfilled (with carrier/AWB if known).</summary>
    Task SendOrderShippedAsync(Order order, Shipment shipment, CancellationToken ct = default);
}
