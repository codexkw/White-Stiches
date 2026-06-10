using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Web.Infrastructure;
using WhiteStiches.Web.Models.Cart;

namespace WhiteStiches.Web.Controllers;

/// <summary>
/// Server-side cart: page, line-item mutations, discount codes, and cart options.
/// The current request's cart is always resolved through <see cref="ICurrentCartAccessor"/>
/// (logged-in user or guest cookie). All mutations follow POST-redirect-GET with
/// one-shot feedback via TempData["CartMessage"] / TempData["CartError"].
/// </summary>
public class CartController(
    ICurrentCartAccessor currentCart,
    ICartService cartService,
    WhiteStichesDbContext db) : Controller
{
    [HttpGet("cart")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cart = await currentCart.GetCartAsync(ct);
        var summary = await cartService.GetSummaryAsync(cart.Id, ct);
        return View(new CartIndexViewModel { Cart = cart, Summary = summary });
    }

    [HttpPost("cart/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(int variantId, int quantity = 1, string? returnUrl = null, CancellationToken ct = default)
    {
        var cart = await currentCart.GetCartAsync(ct);
        try
        {
            await cartService.AddItemAsync(cart.Id, variantId, quantity, ct);
            TempData["CartMessage"] = "added";
        }
        catch (DbUpdateException)
        {
            TempData["CartError"] = "We couldn't add that piece to your bag. Please try again.";
        }
        catch (InvalidOperationException)
        {
            TempData["CartError"] = "We couldn't add that piece to your bag. Please try again.";
        }

        return RedirectBack(returnUrl);
    }

    [HttpPost("cart/items/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(int cartItemId, int quantity, CancellationToken ct)
    {
        var cart = await currentCart.GetCartAsync(ct);
        await cartService.UpdateItemQuantityAsync(cart.Id, cartItemId, quantity, ct);
        TempData["CartMessage"] = quantity <= 0 ? "removed" : "updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cart/items/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int cartItemId, CancellationToken ct)
    {
        var cart = await currentCart.GetCartAsync(ct);
        await cartService.RemoveItemAsync(cart.Id, cartItemId, ct);
        TempData["CartMessage"] = "removed";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cart/discount")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyDiscount(string? code, string? returnUrl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["CartError"] = "Enter a promo or gift card code.";
            return RedirectBack(returnUrl);
        }

        var cart = await currentCart.GetCartAsync(ct);
        try
        {
            await cartService.ApplyDiscountCodeAsync(cart.Id, code.Trim(), ct);
            TempData["CartMessage"] = "discount_applied";
        }
        catch (InvalidOperationException ex)
        {
            TempData["CartError"] = ex.Message switch
            {
                "not_found" => "That code is not valid.",
                "inactive" => "That code is no longer active.",
                "not_started" => "That code is not active yet.",
                "expired" => "That code has expired.",
                "usage_limit" => "That code has reached its usage limit.",
                "min_purchase" => "Your bag has not reached the minimum for this code.",
                "min_quantity" => "Your bag does not have enough items for this code.",
                _ => "That code is not valid."
            };
        }

        return RedirectBack(returnUrl);
    }

    [HttpPost("cart/discount/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDiscount(string? returnUrl = null, CancellationToken ct = default)
    {
        var cart = await currentCart.GetCartAsync(ct);
        await cartService.RemoveDiscountCodeAsync(cart.Id, ct);
        TempData["CartMessage"] = "discount_removed";
        return RedirectBack(returnUrl);
    }

    [HttpPost("cart/options")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOptions(string? note, bool giftWrap = false, CancellationToken ct = default)
    {
        // The accessor returns the cart tracked by this request's scoped DbContext,
        // so setting properties and saving here persists without a dedicated service.
        var cart = await currentCart.GetCartAsync(ct);

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > 500 }) note = note[..500];

        cart.Note = note;
        cart.GiftWrap = giftWrap;
        cart.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        TempData["CartMessage"] = "saved";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
