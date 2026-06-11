using Ganss.Xss;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Infrastructure.Services;

/// <summary>
/// HtmlSanitizer-backed implementation with an allowlist tuned for the kind of formatting the
/// admin WYSIWYG produces (Phase 1E‑2). Everything else — scripts, event handlers, styles, iframes,
/// data:/javascript: URLs — is stripped, so the stored HTML is safe to render with <c>@Html.Raw</c>.
/// </summary>
public sealed class RichTextSanitizer : IRichTextSanitizer
{
    private static readonly string[] AllowedTags =
    [
        "p", "br", "span", "div", "strong", "b", "em", "i", "u", "s",
        "h2", "h3", "h4", "ul", "ol", "li", "blockquote", "a", "hr"
    ];

    private readonly HtmlSanitizer _sanitizer;

    public RichTextSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        _sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTags) _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("title");
        _sanitizer.AllowedAttributes.Add("dir");
        _sanitizer.AllowedAttributes.Add("target");
        _sanitizer.AllowedAttributes.Add("rel");

        _sanitizer.AllowedCssProperties.Clear();

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        // Drop a forbidden tag but keep its readable text rather than deleting the content.
        _sanitizer.KeepChildNodes = true;
    }

    public string Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
}
