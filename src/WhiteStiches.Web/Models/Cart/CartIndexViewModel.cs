using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Web.Models.Cart;

/// <summary>View model for the cart page (GET /cart): the resolved cart plus computed totals.</summary>
public class CartIndexViewModel
{
    public required WhiteStiches.Core.Entities.ShoppingCart.Cart Cart { get; init; }
    public required CartSummary Summary { get; init; }

    /// <summary>
    /// Cross-sell products for the "Complete the bag" grid — real catalog items related to
    /// what's already in the bag (never the old placeholder cards). Empty when the catalog
    /// has nothing else to suggest, in which case the section is not rendered.
    /// </summary>
    public IReadOnlyList<Product> Recommendations { get; init; } = [];
}
