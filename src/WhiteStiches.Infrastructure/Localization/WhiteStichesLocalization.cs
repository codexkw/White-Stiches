using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace WhiteStiches.Infrastructure.Localization;

/// <summary>
/// Shared bilingual (English + Arabic) localization wiring for both the storefront and the
/// back office (Phase 1E‑3). There is no API layer, so both apps register the same options and
/// resolve their own <c>Resources/SharedResource.{culture}.resx</c> dictionaries.
/// </summary>
public static class WhiteStichesLocalization
{
    public const string DefaultCulture = "en";

    /// <summary>The only two cultures the platform ships: English and Kuwaiti Arabic.</summary>
    public static readonly string[] SupportedCultures = ["en", "ar"];

    /// <summary>Registers resx-backed string localization (keys live under <c>/Resources</c>).</summary>
    public static IServiceCollection AddWhiteStichesLocalization(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services;
    }

    /// <summary>
    /// Culture resolution order is the framework default: explicit <c>?culture=</c> query → the
    /// <c>.AspNetCore.Culture</c> cookie (written by the language switcher / on login from the
    /// user's saved preference) → the <c>Accept-Language</c> header → English.
    /// </summary>
    public static RequestLocalizationOptions BuildOptions()
    {
        // The UI (resource) culture switches between English and Arabic so translations, Arabic DB
        // content, and RTL all follow the chosen language (everything keys off CurrentUICulture).
        //
        // The *formatting* culture is deliberately pinned to English everywhere: numbers and dates
        // stay Latin / invariant-style (KWD 3-decimal, "." decimal separator) so numeric model
        // binding round-trips regardless of UI language. This matters because under ICU, Arabic's
        // decimal separator is U+066B ("٫") — so once a user is in Arabic, the MVC decimal binder
        // rejects invariant inputs like "5.000"/"12.500" and every price/amount field silently fails
        // to save. Pinning the format culture to "en" (only the UI culture is allowed to be Arabic)
        // avoids that and matches the house convention of Latin-only numerals.
        var formatting = new List<CultureInfo> { new(DefaultCulture) };               // en only
        var uiCultures = SupportedCultures.Select(c => new CultureInfo(c)).ToList();  // en + ar
        return new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(DefaultCulture, DefaultCulture),
            SupportedCultures = formatting,
            SupportedUICultures = uiCultures,
            ApplyCurrentCultureToResponseHeaders = true
        };
    }

    /// <summary>
    /// Persists the chosen language for a year via the standard ASP.NET culture cookie. Marked
    /// essential so it survives even when the visitor rejects optional cookies.
    /// </summary>
    public static void WriteCultureCookie(HttpContext http, string? langCode)
    {
        var culture = Normalize(langCode);
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false,
                Path = "/"
            });
    }

    /// <summary>Maps any incoming language code to a supported culture, defaulting to English.</summary>
    public static string Normalize(string? langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode)) return DefaultCulture;
        return langCode.Trim().StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : DefaultCulture;
    }

    /// <summary>True when the active UI culture renders right-to-left (Arabic).</summary>
    public static bool IsRightToLeft => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
}
