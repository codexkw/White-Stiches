namespace WhiteStiches.Web.Models.Layout;

/// <summary>
/// Admin-managed store chrome surfaced in the shared layout: announcement-bar messages,
/// social links, and the WhatsApp contact. Populated from <c>ISettingsService</c> by
/// <c>SiteChromeViewComponent</c> so editing these in Admin is reflected on the storefront.
/// </summary>
public class SiteChromeViewModel
{
    public string? InstagramUrl { get; init; }
    public string? TikTokUrl { get; init; }
    public string? PinterestUrl { get; init; }

    /// <summary>Raw WhatsApp number exactly as entered in Admin (any format).</summary>
    public string? WhatsAppNumber { get; init; }

    /// <summary>Click-to-chat link (<c>https://wa.me/&lt;digits&gt;</c>), or null when no number is set.</summary>
    public string? WhatsAppLink { get; init; }

    /// <summary>Header-bar ticker messages in the current language (admin-authored). Empty falls back to defaults.</summary>
    public IReadOnlyList<string> HeaderTicker { get; init; } = [];

    /// <summary>Whether the header ticker is shown (admin toggle; defaults on).</summary>
    public bool HeaderTickerEnabled { get; init; } = true;

    /// <summary>Hero marquee messages in the current language (admin-authored). Empty falls back to defaults.</summary>
    public IReadOnlyList<string> HeroTicker { get; init; } = [];

    /// <summary>Whether the hero marquee (under the banner) is shown (admin toggle; defaults on).</summary>
    public bool HeroTickerEnabled { get; init; } = true;

    public bool HasAnySocial =>
        !string.IsNullOrWhiteSpace(InstagramUrl)
        || !string.IsNullOrWhiteSpace(TikTokUrl)
        || !string.IsNullOrWhiteSpace(PinterestUrl)
        || !string.IsNullOrWhiteSpace(WhatsAppLink);
}
