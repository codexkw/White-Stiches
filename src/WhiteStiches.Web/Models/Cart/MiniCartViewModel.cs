using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Cart;

/// <summary>View model for the global mini-cart drawer view component.</summary>
public class MiniCartViewModel
{
    public required IReadOnlyList<WhiteStiches.Core.Entities.ShoppingCart.CartItem> Items { get; init; }
    public required CartSummary Summary { get; init; }
}
