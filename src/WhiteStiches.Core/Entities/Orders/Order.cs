using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Orders;

/// <summary>
/// An order with snapshotted amounts and shipping address. Line items snapshot product data
/// so later catalog edits never rewrite order history.
/// </summary>
public class Order : BaseEntity
{
    /// <summary>Human-facing unique number (e.g., "WS-10001").</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Null for guest checkout.</summary>
    public Guid? UserId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    /// <summary>Customer's language at purchase time — drives notification language ("en"/"ar").</summary>
    public string LanguageCode { get; set; } = "en";

    public string Currency { get; set; } = "KWD";

    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public FulfillmentStatus FulfillmentStatus { get; set; } = FulfillmentStatus.Unfulfilled;

    public OrderChannel Channel { get; set; } = OrderChannel.Web;

    /// <summary>Draft orders (built by staff for Instagram/WhatsApp sales) until the customer pays via payment link.</summary>
    public bool IsDraft { get; set; }

    public decimal Subtotal { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GiftWrapFee { get; set; }
    public decimal Total { get; set; }

    public int? DiscountCodeId { get; set; }

    /// <summary>Snapshot of the code string applied at purchase time.</summary>
    public string? DiscountCodeSnapshot { get; set; }

    public bool GiftWrap { get; set; }
    public string? CustomerNote { get; set; }
    public string? InternalNote { get; set; }

    // ---- Shipping address snapshot (Kuwait structure) ----
    public string ShipFirstName { get; set; } = string.Empty;
    public string ShipLastName { get; set; } = string.Empty;
    public string ShipPhone { get; set; } = string.Empty;
    public string ShipCountry { get; set; } = "KW";
    public string ShipGovernorate { get; set; } = string.Empty;
    public string ShipArea { get; set; } = string.Empty;
    public string ShipBlock { get; set; } = string.Empty;
    public string ShipStreet { get; set; } = string.Empty;
    public string ShipBuilding { get; set; } = string.Empty;
    public string? ShipFloor { get; set; }
    public string? ShipApartment { get; set; }
    public string? ShipDirections { get; set; }

    public string? ShippingMethodName { get; set; }

    public DateTime? PlacedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderEvent> Events { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Refund> Refunds { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
    public ICollection<ReturnRequest> ReturnRequests { get; set; } = [];
}
