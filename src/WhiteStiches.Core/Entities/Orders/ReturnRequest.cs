using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Orders;

/// <summary>Customer return request. Phase 1 approval is manual in Admin.</summary>
public class ReturnRequest : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? UserId { get; set; }

    /// <summary>Human-facing return number (e.g., "RMA-1001").</summary>
    public string RmaNumber { get; set; } = string.Empty;

    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;

    public string? CustomerReason { get; set; }

    /// <summary>Return method: "pickup" or "dropoff".</summary>
    public string? Method { get; set; }

    public string? StaffNote { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public ICollection<ReturnItem> Items { get; set; } = [];
}
