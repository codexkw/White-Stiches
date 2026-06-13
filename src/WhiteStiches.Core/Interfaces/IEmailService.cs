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

    /// <summary>Delivery confirmation — sent when an order is marked delivered.</summary>
    Task SendOrderDeliveredAsync(Order order, CancellationToken ct = default);

    /// <summary>Cancellation notice — sent when an order is cancelled (reason from <see cref="Order.CancelReason"/>).</summary>
    Task SendOrderCancelledAsync(Order order, CancellationToken ct = default);

    /// <summary>Refund confirmation — sent when a refund is issued (standalone or for a return).</summary>
    Task SendOrderRefundedAsync(Order order, Refund refund, CancellationToken ct = default);

    /// <summary>Payment-not-completed nudge — sent when a Tap charge is declined/abandoned.</summary>
    Task SendPaymentFailedAsync(Order order, CancellationToken ct = default);

    /// <summary>Return-request acknowledgement — sent when a customer submits an RMA.</summary>
    Task SendReturnRequestedAsync(Order order, ReturnRequest request, CancellationToken ct = default);

    /// <summary>Return-approved notice (with pickup/drop-off instructions).</summary>
    Task SendReturnApprovedAsync(Order order, ReturnRequest request, CancellationToken ct = default);

    /// <summary>Return-rejected notice (with the staff reason).</summary>
    Task SendReturnRejectedAsync(Order order, ReturnRequest request, CancellationToken ct = default);

    /// <summary>Return-received notice — the parcel arrived and the refund is being processed.</summary>
    Task SendReturnReceivedAsync(Order order, ReturnRequest request, CancellationToken ct = default);

    // ---- Staff alerts (sent to Smtp:AdminNotifyEmail; no-op when unset) ----

    /// <summary>Alerts staff that a new paid order arrived.</summary>
    Task SendNewOrderNotificationAsync(Order order, CancellationToken ct = default);

    /// <summary>Alerts staff that a new return request needs review.</summary>
    Task SendNewReturnNotificationAsync(Order order, ReturnRequest request, CancellationToken ct = default);

    /// <summary>Alerts staff that a Tap charge was captured but its amount didn't match the order total.</summary>
    Task SendChargeMismatchAlertAsync(Order order, string chargeId, decimal capturedAmount, CancellationToken ct = default);
}
