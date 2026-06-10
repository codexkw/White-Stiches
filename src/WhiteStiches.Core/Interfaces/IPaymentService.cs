using WhiteStiches.Core.Models.Payments;

namespace WhiteStiches.Core.Interfaces;

/// <summary>
/// Order-aware payment orchestration on top of <see cref="IPaymentGateway"/>:
/// starts a charge for a placed order, and idempotently turns a confirmed charge
/// into a paid order. Finalization marks the payment captured, the order paid,
/// and decrements stock exactly once — whether the trigger is the browser return
/// or the server webhook (both can fire, in any order).
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Starts a hosted charge for an already-created order and stores the gateway
    /// charge id on its payment. On failure the payment is marked failed. Returns the
    /// raw gateway result so the caller can redirect to the hosted page.
    /// </summary>
    Task<PaymentChargeResult> StartChargeForOrderAsync(int orderId, string redirectUrl, string? webhookUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Idempotently captures the payment, marks the order paid, and decrements stock once.
    /// When <paramref name="capturedAmount"/> is supplied it is reconciled against the order
    /// total; a mismatch is left for manual review (not marked paid).
    /// </summary>
    Task<OrderFinalizeResult> FinalizeCapturedChargeAsync(string chargeId, decimal? capturedAmount = null,
        string? responseJson = null, CancellationToken ct = default);

    /// <summary>Marks the matching payment failed (no stock movement). Never downgrades an already-paid order.</summary>
    Task MarkChargeFailedAsync(string chargeId, string? rawStatus, string? responseJson = null,
        CancellationToken ct = default);
}
