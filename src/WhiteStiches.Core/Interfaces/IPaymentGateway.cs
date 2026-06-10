using WhiteStiches.Core.Models.Payments;

namespace WhiteStiches.Core.Interfaces;

/// <summary>
/// Provider-agnostic payment gateway (implemented by Tap). Hosted redirect flow:
/// create a charge → redirect the customer to the hosted page → confirm via the
/// browser return (retrieve) and/or the server webhook. Refunds are issued from
/// the back office. The secret key stays server-side; card data never touches us.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>True when a secret key is configured. Checkout falls back to a manual flow otherwise.</summary>
    bool IsConfigured { get; }

    /// <summary>Creates a hosted charge. On success the result carries the redirect URL and gateway charge id.</summary>
    Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default);

    /// <summary>Reads the authoritative charge status (used after the browser returns).</summary>
    Task<PaymentChargeStatus> RetrieveChargeAsync(string chargeId, CancellationToken ct = default);

    /// <summary>Issues a full or partial refund against a captured charge.</summary>
    Task<PaymentRefundResult> CreateRefundAsync(PaymentRefundRequest request, CancellationToken ct = default);

    /// <summary>Parses an inbound webhook body and verifies its signature header (Tap: HMAC-SHA256 "hashstring").</summary>
    PaymentWebhookResult ParseAndVerifyWebhook(string rawBody, string? signatureHeader);
}
