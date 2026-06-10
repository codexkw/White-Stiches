using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Web.Models;

namespace WhiteStiches.Web.Controllers;

public class HomeController : Controller
{
    // Session cookie (no expiry): the splash greets each browsing session once,
    // then every entry point behaves normally. Deep links are never gated.
    private const string IntroSeenCookie = "ws_intro";

    public IActionResult Index()
    {
        if (!Request.Cookies.ContainsKey(IntroSeenCookie))
        {
            return RedirectToAction(nameof(Intro));
        }

        return View();
    }

    [Route("intro")]
    public IActionResult Intro()
    {
        // Set on serve, not on exit — the gate must open even if JS never runs.
        Response.Cookies.Append(IntroSeenCookie, "1", new CookieOptions
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
