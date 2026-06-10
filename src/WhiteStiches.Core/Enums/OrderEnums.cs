namespace WhiteStiches.Core.Enums;

/// <summary>Customer-facing order lifecycle (PRD stepper: Placed → Paid → Fulfilled → Shipped → Delivered).</summary>
public enum OrderStatus
{
    Placed = 0,
    Paid = 1,
    Fulfilled = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Paid = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
    Failed = 5
}

public enum FulfillmentStatus
{
    Unfulfilled = 0,
    PartiallyFulfilled = 1,
    Fulfilled = 2
}

/// <summary>Status of an individual payment transaction at the gateway (Tap).</summary>
public enum TransactionStatus
{
    Initiated = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Voided = 4,
    Refunded = 5
}

public enum RefundStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}

public enum ShipmentStatus
{
    Pending = 0,
    LabelCreated = 1,
    PickedUp = 2,
    InTransit = 3,
    OutForDelivery = 4,
    Delivered = 5,
    DeliveryFailed = 6,
    ReturnedToSender = 7
}

public enum ReturnStatus
{
    Pending = 0,
    Approved = 1,
    Received = 2,
    Refunded = 3,
    Rejected = 4
}

public enum OrderChannel
{
    Web = 0,
    Draft = 1,
    Instagram = 2,
    WhatsApp = 3,
    Phone = 4
}
