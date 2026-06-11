namespace WhiteStiches.Core.Interfaces;

/// <summary>
/// Sanitizes admin-authored rich-text HTML (product/journal/page/collection bodies) before it is
/// persisted and later rendered raw on the storefront. Closes the stored-XSS vector opened by the
/// WYSIWYG editor (Phase 1E‑2).
/// </summary>
public interface IRichTextSanitizer
{
    /// <summary>Returns the input HTML with disallowed tags, attributes, schemes and scripts removed.</summary>
    string Sanitize(string? html);
}
