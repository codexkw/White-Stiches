using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Models.Content;

namespace WhiteStiches.Web.Controllers;

public class PagesController(IContentService contentService, IOrderService orderService) : Controller
{
    [Route("about")]
    public Task<IActionResult> About(CancellationToken ct = default) => RenderContentPageAsync("about", "About", ct);

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
    public Task<IActionResult> Faq(CancellationToken ct = default) => RenderContentPageAsync("faq", "Faq", ct);

    [Route("size-guide")]
    public Task<IActionResult> SizeGuide(CancellationToken ct = default) => RenderContentPageAsync("size-guide", "SizeGuide", ct);

    [Route("shipping")]
    public Task<IActionResult> Shipping(CancellationToken ct = default) => RenderContentPageAsync("shipping", "Shipping", ct);

    [Route("returns-policy")]
    public Task<IActionResult> ReturnsPolicy(CancellationToken ct = default) => RenderContentPageAsync("returns-policy", "ReturnsPolicy", ct);

    [Route("privacy")]
    public Task<IActionResult> Privacy(CancellationToken ct = default) => RenderContentPageAsync("privacy", "Privacy", ct);

    [Route("terms")]
    public Task<IActionResult> Terms(CancellationToken ct = default) => RenderContentPageAsync("terms", "Terms", ct);

    [Route("cookies")]
    public Task<IActionResult> Cookies(CancellationToken ct = default) => RenderContentPageAsync("cookies", "Cookies", ct);

    /// <summary>Generic admin-authored page (/page/{slug}); 404 when the page is missing or unpublished.</summary>
    [HttpGet("page/{slug}")]
    public async Task<IActionResult> Page(string slug, CancellationToken ct = default)
    {
        var page = await contentService.GetPageBySlugAsync(slug, ct);
        return page is null ? NotFound() : View("Content", page);
    }

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

    /// <summary>
    /// Renders a static content page from its admin-authored <see cref="StaticPage"/> when one is
    /// published for the slug and has a body; otherwise falls back to the bespoke Razor design, so
    /// the storefront is never worse off than before the CMS read-path was wired in.
    /// </summary>
    private async Task<IActionResult> RenderContentPageAsync(string slug, string fallbackView, CancellationToken ct)
    {
        var page = await contentService.GetPageBySlugAsync(slug, ct);
        var hasBody = page is not null &&
            (!string.IsNullOrWhiteSpace(page.BodyEn) || !string.IsNullOrWhiteSpace(page.BodyAr));
        return hasBody ? View("Content", page) : View(fallbackView);
    }
}
