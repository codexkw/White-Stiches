namespace WhiteStiches.Core.Models.Payments;

/// <summary>Normalized charge state, mapped from provider-specific status strings (Tap: CAPTURED/INITIATED/...).</summary>
public enum GatewayChargeState
{
    Pending,
    Captured,
    Failed
}

/// <summary>Provider-agnostic request to start a hosted (redirect) payment for an order.</summary>
public record PaymentChargeRequest
{
    public required decimal Amount { get; init; }
    public string Currency { get; init; } = "KWD";
    public required string OrderNumber { get; init; }
    public string? Description { get; init; }

    public string CustomerFirstName { get; init; } = string.Empty;
    public string? CustomerLastName { get; init; }
    public string? CustomerEmail { get; init; }
    public int CustomerPhoneCountryCode { get; init; } = 965;
    public string? CustomerPhoneNumber { get; init; }

    /// <summary>Browser return URL; the gateway appends its charge id on return.</summary>
    public required string RedirectUrl { get; init; }

    /// <summary>Server-to-server webhook URL (must be public HTTPS in production).</summary>
    public string? WebhookUrl { get; init; }
}

/// <summary>Result of creating a charge. On success, redirect the browser to <see cref="HostedPaymentUrl"/>.</summary>
public record PaymentChargeResult
{
    public bool Success { get; init; }
    public string? ChargeId { get; init; }
    public string? HostedPaymentUrl { get; init; }
    public string? RawJson { get; init; }
    public string? Error { get; init; }
}

/// <summary>Authoritative charge status read back from the gateway (GET retrieve).</summary>
public record PaymentChargeStatus
{
    public string? ChargeId { get; init; }
    public GatewayChargeState State { get; init; }
    public string RawStatus { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? RawJson { get; init; }

    public bool IsCaptured => State == GatewayChargeState.Captured;
}

/// <summary>Provider-agnostic refund request against a captured charge. Partial refunds pass a smaller amount.</summary>
public record PaymentRefundRequest
{
    public required string ChargeId { get; init; }
    public required decimal Amount { get; init; }
    public string Currency { get; init; } = "KWD";

    /// <summary>Tap reason vocabulary: duplicate | fraudulent | requested_by_customer.</summary>
    public string Reason { get; init; } = "requested_by_customer";
    public string? Description { get; init; }
}

public record PaymentRefundResult
{
    public bool Success { get; init; }
    public string? RefundId { get; init; }
    public string? RawStatus { get; init; }
    public string? RawJson { get; init; }
    public string? Error { get; init; }
}

/// <summary>Outcome of parsing + verifying an inbound gateway webhook.</summary>
public record PaymentWebhookResult
{
    public bool IsValid { get; init; }
    public string? ChargeId { get; init; }
    public GatewayChargeState State { get; init; }
    public string RawStatus { get; init; } = string.Empty;

    /// <summary>The captured amount from the (signed) webhook body, for reconciliation against the order total.</summary>
    public decimal Amount { get; init; }

    public bool IsCaptured => State == GatewayChargeState.Captured;
}
