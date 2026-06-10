namespace WhiteStiches.Web.Infrastructure;

/// <summary>Guest cart identity cookie. Stores the Cart.Token GUID — never the database id.</summary>
public static class CartCookieHelper
{
    public const string CookieName = "ws_cart";

    public static Guid? GetToken(HttpContext ctx) =>
        ctx.Request.Cookies.TryGetValue(CookieName, out var value) && Guid.TryParse(value, out var token)
            ? token
            : null;

    public static void SetToken(HttpContext ctx, Guid token) =>
        ctx.Response.Cookies.Append(CookieName, token.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(90)
        });

    public static void Clear(HttpContext ctx) => ctx.Response.Cookies.Delete(CookieName);
}
