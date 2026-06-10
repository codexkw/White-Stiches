using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Orders;

/// <summary>A payment transaction at the gateway. Card data never touches our servers (Tap hosted fields).</summary>
public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string Provider { get; set; } = "Tap";

    /// <summary>Payment method key: "knet", "visa", "mastercard", "applepay", "googlepay", "cod".</summary>
    public string Method { get; set; } = string.Empty;

    public TransactionStatus Status { get; set; } = TransactionStatus.Initiated;

    /// <summary>Gateway charge identifier (Tap charge id) for reconciliation and refunds.</summary>
    public string? GatewayTransactionId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KWD";

    /// <summary>Raw gateway response payload (JSON) for audit/debugging.</summary>
    public string? ResponseJson { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}
