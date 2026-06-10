using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Orders;

/// <summary>A shipment (full or partial fulfilment) behind the provider-agnostic delivery interface.</summary>
public class Shipment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string? Carrier { get; set; }

    /// <summary>Air Waybill / tracking number.</summary>
    public string? AwbNumber { get; set; }

    public string? TrackingUrl { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
}
