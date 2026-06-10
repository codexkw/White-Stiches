using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Cart;

/// <summary>View model for the cart page (GET /cart): the resolved cart plus computed totals.</summary>
public class CartIndexViewModel
{
    public required WhiteStiches.Core.Entities.ShoppingCart.Cart Cart { get; init; }
    public required CartSummary Summary { get; init; }
}
