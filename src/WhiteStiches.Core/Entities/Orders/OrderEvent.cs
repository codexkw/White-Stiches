namespace WhiteStiches.Core.Entities.Orders;

/// <summary>Append-only order timeline entry (status changes, payments, notes, messages to customer).</summary>
public class OrderEvent : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>Event kind: "placed", "payment", "fulfillment", "shipment", "refund", "return", "note", "message", "system".</summary>
    public string Kind { get; set; } = "system";

    public string Description { get; set; } = string.Empty;

    /// <summary>Staff author; null for system/customer events.</summary>
    public Guid? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
}
