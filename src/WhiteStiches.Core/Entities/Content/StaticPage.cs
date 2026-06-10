namespace WhiteStiches.Core.Entities.Content;

/// <summary>Admin-editable static/policy page (About, Shipping, Returns, Privacy, Terms, Cookies, FAQ...).</summary>
public class StaticPage : BaseEntity
{
    public string Slug { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Long-form body (HTML).</summary>
    public string? BodyEn { get; set; }
    public string? BodyAr { get; set; }

    public string? SeoTitleEn { get; set; }
    public string? SeoTitleAr { get; set; }
    public string? SeoDescriptionEn { get; set; }
    public string? SeoDescriptionAr { get; set; }

    public bool IsPublished { get; set; } = true;
}
