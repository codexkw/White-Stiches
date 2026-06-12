using System.Security.Claims;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Web.Infrastructure;

/// <summary>
/// Storefront maintenance gate. When the <c>store.maintenance_mode</c> setting is on, public
/// requests are redirected to the branded <c>/maintenance</c> page so the store can actually be
/// taken offline from the Admin panel. Always allowed through: the maintenance page itself, the
/// culture switch, and static assets (so the page renders and can switch language). Signed-in
/// staff bypass the gate so they can preview/work on the live site while it is closed.
///
/// The flag is read through <see cref="ISettingsService"/>, which caches for ~5 minutes — so a
/// toggle in Admin takes effect on the storefront within that window (separate process cache).
/// </summary>
public class MaintenanceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISettingsService settings)
    {
        if (IsAlwaysAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        var raw = (await settings.GetAsync(SettingKeys.MaintenanceMode, context.RequestAborted))?
            .Trim().ToLowerInvariant();
        var enabled = raw is "true" or "1" or "on" or "yes";

        if (!enabled || IsStaff(context.User))
        {
            await next(context);
            return;
        }

        context.Response.Redirect("/maintenance");
    }

    /// <summary>Staff (any back-office role) bypass the gate to preview the live site.</summary>
    private static bool IsStaff(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && AppRoles.StaffRoles.Any(user.IsInRole);

    private static bool IsAlwaysAllowed(PathString path) =>
        path.StartsWithSegments("/maintenance") ||
        path.StartsWithSegments("/set-culture") ||
        path.StartsWithSegments("/css") ||
        path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/lib") ||
        path.StartsWithSegments("/assets") ||
        path.StartsWithSegments("/media") ||
        path.StartsWithSegments("/.well-known") ||
        path == "/favicon.ico";
}
