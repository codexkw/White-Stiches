using Microsoft.AspNetCore.Mvc;

namespace WhiteStiches.Web.Controllers;

public class JournalController : Controller
{
    [Route("journal")]
    public IActionResult Index() => View();

    [Route("journal/post")]
    public IActionResult Post() => View();
}
