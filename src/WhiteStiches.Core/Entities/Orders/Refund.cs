using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Orders;

/// <summary>Full or partial refund to the original payment method.</summary>
public class Refund : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public decimal Amount { get; set; }
    public string? Reason { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    /// <summary>Gateway refund identifier (Tap refund id).</summary>
    public string? GatewayRefundId { get; set; }

    public Guid? StaffUserId { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
