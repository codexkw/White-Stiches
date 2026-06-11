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
        var cultures = SupportedCultures.Select(c => new CultureInfo(c)).ToList();
        return new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(DefaultCulture),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
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
