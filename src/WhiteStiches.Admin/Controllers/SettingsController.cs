using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Store settings panels (AD-SET-01/04): per-group forms over the key-value store.</summary>
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.Admin)]
[Route("settings")]
public class SettingsController(ISettingsService settings, IAuditService audit) : Controller
{
    /// <summary>Panel group (query "group") → setting keys it owns.</summary>
    private static readonly Dictionary<string, string[]> PanelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["store"] =
        [
            SettingKeys.StoreNameEn, SettingKeys.StoreNameAr,
            SettingKeys.ContactEmail, SettingKeys.ContactPhone, SettingKeys.WhatsAppNumber
        ],
        ["social"] = [SettingKeys.InstagramUrl, SettingKeys.TikTokUrl, SettingKeys.PinterestUrl],
        ["shipping"] =
        [
            SettingKeys.FreeShippingThreshold, SettingKeys.StandardShippingRate,
            SettingKeys.ExpressShippingRate, SettingKeys.SameDayShippingRate
        ],
        ["cart"] = [SettingKeys.GiftWrapFee],
        ["ticker"] =
        [
            SettingKeys.TickerHeaderEn, SettingKeys.TickerHeaderAr, SettingKeys.TickerHeaderEnabled,
            SettingKeys.TickerHeroEn, SettingKeys.TickerHeroAr, SettingKeys.TickerHeroEnabled
        ],
        ["maintenance"] = [SettingKeys.MaintenanceMode]
    };

    /// <summary>Panel group → group stored on StoreSetting (keeps seeder groupings intact).</summary>
    private static readonly Dictionary<string, string> StorageGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["store"] = "store",
        ["social"] = "store",
        ["shipping"] = "shipping",
        ["cart"] = "cart",
        ["ticker"] = "ticker",
        ["maintenance"] = "store"
    };

    private static readonly string[] DecimalKeys =
    [
        SettingKeys.FreeShippingThreshold, SettingKeys.StandardShippingRate,
        SettingKeys.ExpressShippingRate, SettingKeys.SameDayShippingRate, SettingKeys.GiftWrapFee
    ];

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Settings";
        ViewData["Nav"] = "settings";

        var values = new Dictionary<string, string?>();
        foreach (var key in PanelKeys.Values.SelectMany(k => k))
        {
            values[key] = await settings.GetAsync(key, ct);
        }

        return View(new SettingsIndexViewModel { Values = values });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? group, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(group) || !PanelKeys.TryGetValue(group, out var keys))
        {
            TempData["Err"] = "Unknown settings group.";
            return RedirectToAction(nameof(Index));
        }

        var before = new Dictionary<string, string?>();
        var after = new Dictionary<string, string?>();

        foreach (var key in keys)
        {
            // Only touch keys actually present in the post — a partial form never wipes siblings.
            if (!Request.Form.ContainsKey(key))
            {
                continue;
            }

            var incoming = ReadFormValue(key);

            if (DecimalKeys.Contains(key))
            {
                if (!decimal.TryParse(incoming, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                    || amount < 0)
                {
                    TempData["Err"] = $"\"{key}\" must be a non-negative number (e.g. 3.500).";
                    return RedirectToAction(nameof(Index));
                }

                incoming = amount.ToString("0.000", CultureInfo.InvariantCulture);
            }

            var current = await settings.GetAsync(key, ct);
            if (current == incoming)
            {
                continue;
            }

            before[key] = current;
            after[key] = incoming;
        }

        if (after.Count == 0)
        {
            TempData["Ok"] = "No changes to save.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var (key, value) in after)
        {
            await settings.SetAsync(key, value, StorageGroup[group], ct);
        }

        await audit.LogAsync("settings.update", CurrentUserId(), User.Identity?.Name,
            entityType: "StoreSetting", entityId: group.ToLowerInvariant(),
            before: before, after: after,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

        TempData["Ok"] = $"Saved {after.Count} setting{(after.Count == 1 ? "" : "s")}.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Reads a raw form value by setting key. Checkboxes post hidden "false" + checked "true",
    /// so any "true" among the values wins. Announcement textarea is normalized to one message
    /// per line with blanks removed.
    /// </summary>
    private string? ReadFormValue(string key)
    {
        var values = Request.Form[key];

        if (key is SettingKeys.MaintenanceMode
            or SettingKeys.TickerHeaderEnabled
            or SettingKeys.TickerHeroEnabled)
        {
            return values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
                ? "true"
                : "false";
        }

        var raw = values.LastOrDefault()?.Trim();

        if (key is SettingKeys.AnnouncementMessages
                or SettingKeys.TickerHeaderEn or SettingKeys.TickerHeaderAr
                or SettingKeys.TickerHeroEn or SettingKeys.TickerHeroAr
            && raw is not null)
        {
            var lines = raw
                .Split('\n')
                .Select(l => l.Trim().TrimEnd('\r').Trim())
                .Where(l => l.Length > 0);
            return string.Join("\n", lines);
        }

        return raw ?? string.Empty;
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
