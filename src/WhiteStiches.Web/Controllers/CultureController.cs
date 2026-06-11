using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Infrastructure.Localization;

namespace WhiteStiches.Web.Controllers;

/// <summary>Language switcher endpoint (Phase 1E‑3). Sets the culture cookie and, for signed-in
/// customers, persists the choice to their profile so it follows them across devices.</summary>
[AllowAnonymous]
public class CultureController(UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("set-culture")]
    public async Task<IActionResult> Set(string? culture, string? returnUrl = null)
    {
        var lang = WhiteStichesLocalization.Normalize(culture);
        WhiteStichesLocalization.WriteCultureCookie(HttpContext, lang);

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(User);
            if (user is not null && user.PreferredLanguage != lang)
            {
                user.PreferredLanguage = lang;
                await userManager.UpdateAsync(user);
            }
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return Redirect("/");
    }
}
