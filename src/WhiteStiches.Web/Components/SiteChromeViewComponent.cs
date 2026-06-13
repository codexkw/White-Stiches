using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Models.Layout;

namespace WhiteStiches.Web.Components;

/// <summary>
/// Renders admin-managed store chrome in the shared layout — the announcement bar, footer
/// social links, and the floating WhatsApp button — from <see cref="ISettingsService"/>.
/// Invoked once per surface (<c>Announcement</c> / <c>FooterSocial</c> / <c>WhatsAppFloat</c>);
/// the settings reads are memoized in <see cref="HttpContext.Items"/> so all three surfaces on
/// a page share a single batch of lookups. Editing these values in Admin is reflected on the
/// storefront within the settings cache window (~5 minutes).
/// </summary>
public class SiteChromeViewComponent(ISettingsService settings) : ViewComponent
{
    private const string CacheKey = "__ws_site_chrome";

    public async Task<IViewComponentResult> InvokeAsync(string section = "Announcement")
    {
        var model = await GetChromeAsync();
        return View(section, model);
    }

    private async Task<SiteChromeViewModel> GetChromeAsync()
    {
        if (HttpContext.Items.TryGetValue(CacheKey, out var cached) && cached is SiteChromeViewModel existing)
        {
            return existing;
        }

        var ct = HttpContext.RequestAborted;
        var instagram = await settings.GetAsync(SettingKeys.InstagramUrl, ct);
        var tiktok = await settings.GetAsync(SettingKeys.TikTokUrl, ct);
        var pinterest = await settings.GetAsync(SettingKeys.PinterestUrl, ct);
        var whatsapp = await settings.GetAsync(SettingKeys.WhatsAppNumber, ct);
        var announcementRaw = await settings.GetAsync(SettingKeys.AnnouncementMessages, ct);

        var model = new SiteChromeViewModel
        {
            InstagramUrl = Clean(instagram),
            TikTokUrl = Clean(tiktok),
            PinterestUrl = Clean(pinterest),
            WhatsAppNumber = Clean(whatsapp),
            WhatsAppLink = BuildWhatsAppLink(whatsapp),
            Announcements = SplitMessages(announcementRaw),
        };

        HttpContext.Items[CacheKey] = model;
        return model;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>wa.me needs an international number with no symbols — keep digits only.</summary>
    private static string? BuildWhatsAppLink(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var digits = new string(number.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : $"https://wa.me/{digits}";
    }

    /// <summary>Admin stores messages newline-delimited (see SettingsController.ReadFormValue).</summary>
    private static IReadOnlyList<string> SplitMessages(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }
}
