using Microsoft.AspNetCore.Mvc;

namespace WhiteStiches.Web.Controllers;

public class PagesController : Controller
{
    [Route("about")]
    public IActionResult About() => View();

    [Route("contact")]
    public IActionResult Contact() => View();

    [Route("faq")]
    public IActionResult Faq() => View();

    [Route("size-guide")]
    public IActionResult SizeGuide() => View();

    [Route("shipping")]
    public IActionResult Shipping() => View();

    [Route("returns-policy")]
    public IActionResult ReturnsPolicy() => View();

    [Route("privacy")]
    public IActionResult Privacy() => View();

    [Route("terms")]
    public IActionResult Terms() => View();

    [Route("cookies")]
    public IActionResult Cookies() => View();

    [Route("track")]
    public IActionResult Track() => View();
}
