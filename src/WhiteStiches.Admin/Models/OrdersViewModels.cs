using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

public class OrderListViewModel
{
    public PagedResult<Order> Orders { get; init; } = new();
    public OrderStatus? Status { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public OrderChannel? Channel { get; init; }
    public string? Search { get; init; }
}

public class OrderDetailViewModel
{
    public Order Order { get; init; } = null!;

    /// <summary>Sum of captured payments.</summary>
    public decimal TotalPaid { get; init; }

    /// <summary>Sum of completed refunds.</summary>
    public decimal TotalRefunded { get; init; }

    public decimal RemainingRefundable => Math.Max(0, TotalPaid - TotalRefunded);
}

public class DraftListViewModel
{
    public PagedResult<Order> Drafts { get; init; } = new();
    public OrderChannel? Channel { get; init; }
    public string? Search { get; init; }
}

public class DraftEditViewModel
{
    public Order Order { get; init; } = null!;
    public string? ProductSearch { get; init; }
    public IReadOnlyList<Product> ProductResults { get; init; } = [];
}

/// <summary>POST body for /orders/drafts/{id}/update — field names are the form input names.</summary>
public class DraftUpdateForm
{
    public decimal ShippingAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CustomerNote { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ShipFirstName { get; set; }
    public string? ShipLastName { get; set; }
    public string? ShipPhone { get; set; }
    public string? ShipGovernorate { get; set; }
    public string? ShipArea { get; set; }
    public string? ShipBlock { get; set; }
    public string? ShipStreet { get; set; }
    public string? ShipBuilding { get; set; }
    public string? ShipFloor { get; set; }
    public string? ShipApartment { get; set; }
    public string? ShipDirections { get; set; }
}

/// <summary>Maps order enums onto the admin.css badge modifier classes.</summary>
public static class OrderBadges
{
    public static string Status(OrderStatus s) => s switch
    {
        OrderStatus.Placed => "adm-badge--warn",
        OrderStatus.Paid => "adm-badge--ok",
        OrderStatus.Fulfilled => "adm-badge--info",
        OrderStatus.Shipped => "adm-badge--info",
        OrderStatus.Delivered => "adm-badge--ok",
        OrderStatus.Cancelled => "adm-badge--err",
        OrderStatus.Refunded => "adm-badge--err",
        _ => string.Empty
    };

    public static string Payment(PaymentStatus s) => s switch
    {
        PaymentStatus.Pending => "adm-badge--warn",
        PaymentStatus.Authorized => "adm-badge--info",
        PaymentStatus.Paid => "adm-badge--ok",
        PaymentStatus.PartiallyRefunded => "adm-badge--warn",
        PaymentStatus.Refunded => "adm-badge--info",
        PaymentStatus.Failed => "adm-badge--err",
        _ => string.Empty
    };

    public static string Fulfillment(FulfillmentStatus s) => s switch
    {
        FulfillmentStatus.Unfulfilled => "adm-badge--warn",
        FulfillmentStatus.PartiallyFulfilled => "adm-badge--info",
        FulfillmentStatus.Fulfilled => "adm-badge--ok",
        _ => string.Empty
    };

    public static string Shipment(ShipmentStatus s) => s switch
    {
        ShipmentStatus.Pending => "adm-badge--warn",
        ShipmentStatus.LabelCreated => "adm-badge--info",
        ShipmentStatus.PickedUp => "adm-badge--info",
        ShipmentStatus.InTransit => "adm-badge--info",
        ShipmentStatus.OutForDelivery => "adm-badge--info",
        ShipmentStatus.Delivered => "adm-badge--ok",
        ShipmentStatus.DeliveryFailed => "adm-badge--err",
        ShipmentStatus.ReturnedToSender => "adm-badge--err",
        _ => string.Empty
    };

    public static string Transaction(TransactionStatus s) => s switch
    {
        TransactionStatus.Initiated => "adm-badge--warn",
        TransactionStatus.Authorized => "adm-badge--info",
        TransactionStatus.Captured => "adm-badge--ok",
        TransactionStatus.Failed => "adm-badge--err",
        TransactionStatus.Voided => "adm-badge--err",
        TransactionStatus.Refunded => "adm-badge--info",
        _ => string.Empty
    };

    public static string Return(ReturnStatus s) => s switch
    {
        ReturnStatus.Pending => "adm-badge--warn",
        ReturnStatus.Approved => "adm-badge--ok",
        ReturnStatus.Received => "adm-badge--info",
        ReturnStatus.Refunded => "adm-badge--info",
        ReturnStatus.Rejected => "adm-badge--err",
        _ => string.Empty
    };

    public static string Refund(RefundStatus s) => s switch
    {
        RefundStatus.Pending => "adm-badge--warn",
        RefundStatus.Completed => "adm-badge--ok",
        RefundStatus.Failed => "adm-badge--err",
        _ => string.Empty
    };
}
