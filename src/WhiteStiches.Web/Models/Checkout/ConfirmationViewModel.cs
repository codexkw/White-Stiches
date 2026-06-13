using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;

namespace WhiteStiches.Web.Models.Checkout;

/// <summary>Page model for GET /checkout/confirmation/{orderNumber}.</summary>
public class ConfirmationViewModel
{
    public required Order Order { get; init; }

    /// <summary>True for guest orders viewed by an unauthenticated visitor — shows the create-account prompt.</summary>
    public bool ShowCreateAccount { get; init; }

    public IReadOnlyList<Product> CrossSell { get; init; } = [];

    public string PaymentMethodKey =>
        Order.Payments.OrderBy(p => p.Id).FirstOrDefault()?.Method ?? "knet";

    public string PaymentMethodLabel => PaymentMethodKey switch
    {
        "knet" => "KNET",
        "card" => "Credit / debit card",
        "applepay" => "Apple Pay",
        "cod" => "Cash on delivery",
        "tap" => "Tap Payments",
        _ => PaymentMethodKey
    };

    /// <summary>
    /// Customer-facing payment state for the confirmation card. Driven by the order's actual
    /// gateway-updated <see cref="Order.PaymentStatus"/> — NOT the payment method — so a paid
    /// order reads "Payment confirmed" instead of the old hardcoded "Payment pending". Returns a
    /// localizable key (set dynamically, so its EN/AR entries are maintained in SharedResource).
    /// </summary>
    public string PaymentStatusLabel => Order.PaymentStatus switch
    {
        PaymentStatus.Paid or PaymentStatus.Authorized => "Payment confirmed",
        PaymentStatus.Failed => "Payment failed",
        PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded => "Refunded",
        _ => "Payment pending"
    };

    /// <summary>CSS state hook for the payment meta line (is-paid / is-failed / is-refunded / is-pending).</summary>
    public string PaymentStatusModifier => Order.PaymentStatus switch
    {
        PaymentStatus.Paid or PaymentStatus.Authorized => "is-paid",
        PaymentStatus.Failed => "is-failed",
        PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded => "is-refunded",
        _ => "is-pending"
    };

    /// <summary>The "waiting for your bank" notice only makes sense while payment is genuinely pending.</summary>
    public bool IsPaymentPending => Order.PaymentStatus == PaymentStatus.Pending;

    /// <summary>Short method name for chips (e.g., "Standard" from "Standard - 3-5 days").</summary>
    public string ShippingMethodShort =>
        (Order.ShippingMethodName ?? "Standard").Split(" - ")[0];

    public string ShippingPriceLabel =>
        Order.ShippingAmount == 0 ? "Free" : Order.ShippingAmount.ToString("0.000") + " KWD";

    public int ItemCount => Order.Items.Sum(i => i.Quantity);
}
