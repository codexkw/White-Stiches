using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Models;
using WhiteStiches.Web.Models.Home;

namespace WhiteStiches.Web.Controllers;

public class HomeController(IBannerService banners) : Controller
{
    // One-shot "intro pass" cookie: /intro sets it and the home page consumes
    // (deletes) it on render, so the splash plays on EVERY fresh visit to the
    // home page rather than once per session/user. Deep links are never gated.
    private const string IntroPassCookie = "ws_intro";

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        if (!Request.Cookies.ContainsKey(IntroPassCookie))
        {
            return RedirectToAction(nameof(Intro));
        }

        // Consume the one-shot pass so the splash is shown again the next time
        // the home page is opened.
        Response.Cookies.Delete(IntroPassCookie);

        var hero = await banners.GetActiveHeroAsync(ct);
        return View(new HomeViewModel { Hero = hero });
    }

    [Route("intro")]
    public IActionResult Intro()
    {
        // Set on serve, not on exit — the gate must open even if JS never runs.
        Response.Cookies.Append(IntroPassCookie, "1", new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

        return View();
    }

    [Route("not-found")]
    public IActionResult PageNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    [Route("maintenance")]
    public IActionResult Maintenance() => View();

    [Route("design-system")]
    public IActionResult DesignSystem() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
