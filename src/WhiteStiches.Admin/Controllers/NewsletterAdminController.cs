using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Newsletter subscriber list and CSV export (AD-MKT-01).</summary>
[Route("newsletter")]
public class NewsletterAdminController(IMarketingService marketing, IAuditService audit) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Newsletter";
        ViewData["Nav"] = "newsletter";

        var subscribers = await marketing.GetSubscribersAsync(page, 50, ct);

        return View(new NewsletterListViewModel { Subscribers = subscribers });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var subscribers = await marketing.GetAllSubscribersAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("email,language,whatsapp,source,subscribedAtUtc");
        foreach (var s in subscribers)
        {
            sb.Append(Csv(s.Email)).Append(',')
              .Append(Csv(s.LanguageCode)).Append(',')
              .Append(s.WhatsAppOptIn ? "yes" : "no").Append(',')
              .Append(Csv(s.Source)).Append(',')
              .Append(s.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"))
              .AppendLine();
        }

        Guid? userId = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : null;
        await audit.LogAsync("newsletter.export", userId, User.Identity?.Name,
            "NewsletterSubscriber", after: new { count = subscribers.Count }, ct: ct);

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "newsletter-subscribers.csv");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
