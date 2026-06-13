using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Checkout;

/// <summary>Page model for GET /checkout — cart lines, totals, and shipping method rows.</summary>
public class CheckoutViewModel
{
    public CheckoutFormModel Form { get; set; } = new();
    public IReadOnlyList<CheckoutItemViewModel> Items { get; set; } = [];
    public CartSummary Summary { get; set; } = new();
    public IReadOnlyList<ShippingMethodOption> ShippingMethods { get; set; } = [];

    /// <summary>The discount code currently applied to the cart, if any.</summary>
    public string? DiscountCode { get; set; }

    /// <summary>The currently selected delivery method (standard/express/same-day).</summary>
    public string SelectedMethod { get; set; } = "standard";

    /// <summary>Shipping cost for the selected method — what the order is actually charged.</summary>
    public decimal SelectedShipping { get; set; }

    /// <summary>Subtotal − discount + gift wrap (shipping-independent), so the client can recompute live.</summary>
    public decimal BaseTotal { get; set; }

    /// <summary>BaseTotal + SelectedShipping — the method-aware grand total the customer sees and pays.</summary>
    public decimal GrandTotal { get; set; }
}

public class CheckoutItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageAlt { get; set; }

    /// <summary>Variant summary shown under the title (e.g., "Oat · S").</summary>
    public string? VariantLabel { get; set; }

    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class ShippingMethodOption
{
    public string Value { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
}
