using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Infrastructure;
using WhiteStiches.Web.Models.Cart;

namespace WhiteStiches.Web.Components;

/// <summary>
/// Renders the inner content of the global mini-cart drawer (header, free-shipping
/// progress, line items, totals, CTAs). The drawer chrome (aside/backdrop) stays in
/// _Layout so the open/close module in site.js keeps working.
/// </summary>
public class MiniCartViewComponent(ICurrentCartAccessor currentCart, ICartService cartService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var ct = HttpContext.RequestAborted;
        var cart = await currentCart.GetCartAsync(ct);
        var summary = await cartService.GetSummaryAsync(cart.Id, ct);

        return View(new MiniCartViewModel
        {
            Items = cart.Items.OrderBy(i => i.Id).ToList(),
            Summary = summary
        });
    }
}
