using WhiteStiches.Core.Entities.ShoppingCart;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Web.Infrastructure;

/// <summary>
/// Resolves the cart for the current request: by user id when authenticated,
/// otherwise by the guest cookie token. Keeps the cookie in sync with the
/// cart actually returned and caches the cart per request.
/// </summary>
public interface ICurrentCartAccessor
{
    Task<Cart> GetCartAsync(CancellationToken ct = default);
}

public class CurrentCartAccessor(ICartService cartService, IHttpContextAccessor httpContextAccessor) : ICurrentCartAccessor
{
    private Cart? _cart;

    public async Task<Cart> GetCartAsync(CancellationToken ct = default)
    {
        if (_cart is not null) return _cart;

        var ctx = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HttpContext.");

        var userId = ctx.User.GetUserId();
        var guestToken = CartCookieHelper.GetToken(ctx);

        _cart = await cartService.GetOrCreateCartAsync(userId, guestToken, ct);

        // A new cart gets a fresh token — keep the guest cookie pointing at it
        if (userId is null && _cart.Token != guestToken && !ctx.Response.HasStarted)
        {
            CartCookieHelper.SetToken(ctx, _cart.Token);
        }

        return _cart;
    }
}
