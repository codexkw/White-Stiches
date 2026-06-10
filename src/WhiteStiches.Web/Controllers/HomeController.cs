using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Web.Models;

namespace WhiteStiches.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    [Route("intro")]
    public IActionResult Intro() => View();

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
