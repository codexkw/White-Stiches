using Microsoft.AspNetCore.Mvc;

namespace WhiteStiches.Web.Controllers;

public class AccountController : Controller
{
    [Route("account")]
    public IActionResult Index() => View();

    [Route("account/login")]
    public IActionResult Login() => View();

    [Route("account/orders")]
    public IActionResult Orders() => View();

    [Route("account/orders/detail")]
    public IActionResult OrderDetail() => View();

    [Route("account/addresses")]
    public IActionResult Addresses() => View();

    [Route("account/profile")]
    public IActionResult Profile() => View();

    [Route("account/wishlist")]
    public IActionResult Wishlist() => View();

    [Route("account/returns")]
    public IActionResult Returns() => View();
}
