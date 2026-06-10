using Microsoft.AspNetCore.Mvc;

namespace WhiteStiches.Web.Controllers;

public class ShopController : Controller
{
    [Route("collection")]
    public IActionResult Collection() => View();

    [Route("product")]
    public IActionResult Product() => View();

    [Route("search")]
    public IActionResult Search() => View();
}
