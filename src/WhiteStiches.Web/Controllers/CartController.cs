using Microsoft.AspNetCore.Mvc;

namespace WhiteStiches.Web.Controllers;

public class CartController : Controller
{
    [Route("cart")]
    public IActionResult Index() => View();
}
