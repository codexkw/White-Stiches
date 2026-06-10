using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Models.Content;

namespace WhiteStiches.Web.Controllers;

public class PagesController(IContentService contentService, IOrderService orderService) : Controller
{
    [Route("about")]
    public IActionResult About() => View();

    [HttpGet("contact")]
    public IActionResult Contact() => View();

    /// <summary>Contact form submission (SF-STA-02). Redirects back with the success state visible.</summary>
    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(
        string name,
        string email,
        string? phoneCode,
        string? phone,
        string? subject,
        string? orderNumber,
        string message,
        CancellationToken ct = default)
    {
        name = name?.Trim() ?? string.Empty;
        email = email?.Trim() ?? string.Empty;
        message = message?.Trim() ?? string.Empty;

        if (name.Length == 0 || email.Length == 0 || message.Length == 0)
        {
            TempData["ContactError"] = "Please fill in your name, email and message.";
            return RedirectToAction(nameof(Contact));
        }

        var body = message;
        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            body += $"\n\nOrder number: {orderNumber.Trim()}";
        }

        string? fullPhone = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var code = string.IsNullOrWhiteSpace(phoneCode) ? "+965" : phoneCode.Trim();
            fullPhone = $"{code} {phone.Trim()}";
        }

        await contentService.SubmitContactMessageAsync(new ContactMessage
        {
            Name = name,
            Email = email,
            Phone = fullPhone,
            Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
            Body = body
        }, ct);

        TempData["ContactSentName"] = name;
        return RedirectToAction(nameof(Contact));
    }

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

    [HttpGet("track")]
    public IActionResult Track() => View(new TrackViewModel());

    /// <summary>Guest order lookup (SF-STA-06) — order number + the email or phone used at checkout.</summary>
    [HttpPost("track")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(string orderNumber, string contact, CancellationToken ct = default)
    {
        var number = (orderNumber ?? string.Empty).Trim().TrimStart('#');
        var lookup = (contact ?? string.Empty).Trim();

        Order? order = null;
        if (number.Length > 0 && lookup.Length > 0)
        {
            order = await orderService.TrackAsync(number, lookup, ct);
        }

        return View(new TrackViewModel
        {
            Searched = true,
            OrderNumber = number,
            Order = order
        });
    }
}
