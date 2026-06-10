using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Admin.Controllers;

public class DashboardController(WhiteStichesDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";
        ViewData["Nav"] = "dashboard";

        ViewBag.ProductCount = await db.Products.CountAsync();
        ViewBag.OrderCount = await db.Orders.CountAsync();
        ViewBag.CustomerCount = await db.Users.CountAsync(u => !u.IsStaff);
        ViewBag.UnreadMessages = await db.ContactMessages.CountAsync(m => !m.IsRead);
        ViewBag.NewsletterCount = await db.NewsletterSubscribers.CountAsync(s => s.UnsubscribedAtUtc == null);
        ViewBag.PendingReturns = await db.ReturnRequests.CountAsync(r => r.Status == Core.Enums.ReturnStatus.Pending);

        return View();
    }
}
