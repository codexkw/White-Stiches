using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Web.Controllers;

public class NewsletterController(IMarketingService marketingService) : Controller
{
    /// <summary>
    /// Newsletter signup (SF-HOM-05). Sets TempData["NewsletterResult"] = "ok" | "exists" | "invalid"
    /// and redirects back to the page the form was submitted from.
    /// </summary>
    [HttpPost("newsletter")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(string email, bool whatsAppOptIn = false, string? source = null, CancellationToken ct = default)
    {
        email = email?.Trim() ?? string.Empty;

        if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
        {
            TempData["NewsletterResult"] = "invalid";
            return Redirect(GetReturnTarget());
        }

        var created = await marketingService.SubscribeToNewsletterAsync(email, whatsAppOptIn, "en", source, ct);
        TempData["NewsletterResult"] = created ? "ok" : "exists";

        return Redirect(GetReturnTarget());
    }

    /// <summary>Referer when it points at this site; "/" otherwise (never an open redirect).</summary>
    private string GetReturnTarget()
    {
        var referer = Request.Headers.Referer.ToString();
        if (string.IsNullOrEmpty(referer)) return "/";

        if (Url.IsLocalUrl(referer)) return referer;

        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return uri.PathAndQuery;
        }

        return "/";
    }
}
