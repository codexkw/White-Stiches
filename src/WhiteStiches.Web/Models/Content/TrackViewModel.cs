using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;

namespace WhiteStiches.Web.Models.Content;

/// <summary>Guest order tracking page — lookup form, not-found state and the status result.</summary>
public class TrackViewModel
{
    /// <summary>True after a POST lookup (distinguishes the initial form from the not-found state).</summary>
    public bool Searched { get; init; }

    /// <summary>Echo of the order number the visitor searched for (normalised, no leading '#').</summary>
    public string? OrderNumber { get; init; }

    public Order? Order { get; init; }

    public bool NotFound => Searched && Order is null;

    /// <summary>Cancelled / refunded orders render the error-ish state with status text instead of the stepper.</summary>
    public bool IsTerminated => Order is { Status: OrderStatus.Cancelled or OrderStatus.Refunded };

    /// <summary>Stepper index: Placed 0 · Paid 1 · Fulfilled 2 · Shipped 3 · Delivered 4.</summary>
    public int StepIndex => Order is null ? 0 : Math.Clamp((int)Order.Status, 0, 4);

    public Shipment? LatestShipment =>
        Order?.Shipments.OrderByDescending(s => s.CreatedAtUtc).FirstOrDefault();

    /// <summary>Total pieces across all line items.</summary>
    public int PieceCount => Order?.Items.Sum(i => i.Quantity) ?? 0;

    public string StatusLabel => Order?.Status switch
    {
        OrderStatus.Placed => "Placed",
        OrderStatus.Paid => "Confirmed",
        OrderStatus.Fulfilled => "Being prepared",
        OrderStatus.Shipped => "In transit",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Cancelled => "Cancelled",
        OrderStatus.Refunded => "Refunded",
        _ => string.Empty
    };

    public string StatusCopy => Order?.Status switch
    {
        OrderStatus.Placed => "We’ve received your order and sent a confirmation to your email. We’ll let you know the moment it moves.",
        OrderStatus.Paid => "Payment confirmed — our atelier is preparing your pieces.",
        OrderStatus.Fulfilled => "Your pieces are inspected, pressed and packed in our signature tissue — awaiting the courier.",
        OrderStatus.Shipped => "Your order left our Kuwait atelier. The courier will WhatsApp 30 minutes before delivery.",
        OrderStatus.Delivered => "Your order has been delivered. We hope you love every piece.",
        _ => string.Empty
    };
}
