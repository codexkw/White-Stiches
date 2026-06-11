using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Infrastructure.Localization;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Back-office language switcher (Phase 1E‑3). Allowed anonymously so it also works on
/// the login screen; persists the choice for signed-in staff.</summary>
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
        return RedirectToAction("Index", "Dashboard");
    }
}
