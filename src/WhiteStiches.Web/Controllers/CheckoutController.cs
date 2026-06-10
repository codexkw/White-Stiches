using Microsoft.AspNetCore.Mvc;

namespace WhiteStiches.Web.Controllers;

public class CheckoutController : Controller
{
    [Route("checkout")]
    public IActionResult Index() => View();

    [Route("checkout/confirmation")]
    public IActionResult Confirmation() => View();
}
