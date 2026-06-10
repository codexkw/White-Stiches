namespace WhiteStiches.Core.Entities.Orders;

public class ReturnItem : BaseEntity
{
    public int ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;

    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public int Quantity { get; set; } = 1;
    public string? Reason { get; set; }

    /// <summary>Inspection outcome note (condition) recorded on receipt.</summary>
    public string? Condition { get; set; }

    /// <summary>Whether stock was returned to inventory on receipt.</summary>
    public bool Restocked { get; set; }
}
