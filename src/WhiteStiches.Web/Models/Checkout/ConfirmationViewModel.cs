using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;

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
        _ => PaymentMethodKey
    };

    /// <summary>Short method name for chips (e.g., "Standard" from "Standard - 3-5 days").</summary>
    public string ShippingMethodShort =>
        (Order.ShippingMethodName ?? "Standard").Split(" - ")[0];

    public string ShippingPriceLabel =>
        Order.ShippingAmount == 0 ? "Free" : Order.ShippingAmount.ToString("0.000") + " KWD";

    public int ItemCount => Order.Items.Sum(i => i.Quantity);
}
