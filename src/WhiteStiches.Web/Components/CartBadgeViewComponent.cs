using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Web.Infrastructure;

namespace WhiteStiches.Web.Components;

/// <summary>Renders the header cart badge (.hdr__cart-count) with the real item count.</summary>
public class CartBadgeViewComponent(ICurrentCartAccessor currentCart) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var cart = await currentCart.GetCartAsync(HttpContext.RequestAborted);
        return View(cart.Items.Sum(i => i.Quantity));
    }
}
