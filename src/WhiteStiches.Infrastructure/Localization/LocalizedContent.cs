using System.Globalization;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Entities.Orders;

namespace WhiteStiches.Infrastructure.Localization;

/// <summary>
/// Culture-aware accessors for the bilingual content columns (Phase 1E‑3 string sweep). In Arabic
/// these return the <c>…Ar</c> value, falling back to English when a translation is missing — so the
/// storefront finally renders the Arabic catalog/content that was previously dormant in the database.
/// Usage in views: <c>@product.Title()</c>, <c>@category.Name()</c>, <c>@LocalizedContent.Pick(en, ar)</c>.
/// </summary>
public static class LocalizedContent
{
    public static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the Arabic value when the UI is Arabic and it's non-empty; otherwise English.</summary>
    public static string Pick(string? en, string? ar) =>
        IsArabic && !string.IsNullOrWhiteSpace(ar) ? ar! : en ?? string.Empty;

    // ── Catalog ────────────────────────────────────────────────────────────
    public static string Title(this Product p) => Pick(p.TitleEn, p.TitleAr);
    public static string Description(this Product p) => Pick(p.DescriptionEn, p.DescriptionAr);
    public static string MaterialCare(this Product p) => Pick(p.MaterialCareEn, p.MaterialCareAr);
    public static string SizeFit(this Product p) => Pick(p.SizeFitEn, p.SizeFitAr);
    public static string SeoTitle(this Product p) => Pick(p.SeoTitleEn, p.SeoTitleAr);
    public static string SeoDescription(this Product p) => Pick(p.SeoDescriptionEn, p.SeoDescriptionAr);

    public static string Name(this Category c) => Pick(c.NameEn, c.NameAr);
    public static string Description(this Category c) => Pick(c.DescriptionEn, c.DescriptionAr);

    public static string Title(this Collection c) => Pick(c.TitleEn, c.TitleAr);
    public static string Description(this Collection c) => Pick(c.DescriptionEn, c.DescriptionAr);

    public static string Name(this ProductOption o) => Pick(o.NameEn, o.NameAr);
    public static string Alt(this ProductImage i) => Pick(i.AltEn, i.AltAr);

    // ── Content ────────────────────────────────────────────────────────────
    public static string Title(this JournalPost j) => Pick(j.TitleEn, j.TitleAr);
    public static string Excerpt(this JournalPost j) => Pick(j.ExcerptEn, j.ExcerptAr);
    public static string Body(this JournalPost j) => Pick(j.BodyEn, j.BodyAr);
    public static string Name(this JournalCategory c) => Pick(c.NameEn, c.NameAr);

    public static string Title(this StaticPage p) => Pick(p.TitleEn, p.TitleAr);
    public static string Body(this StaticPage p) => Pick(p.BodyEn, p.BodyAr);

    // ── Homepage hero banner ────────────────────────────────────────────────
    public static string Eyebrow(this Banner b) => Pick(b.EyebrowEn, b.EyebrowAr);
    public static string TitleLine1(this Banner b) => Pick(b.TitleLine1En, b.TitleLine1Ar);
    public static string TitleLine2(this Banner b) => Pick(b.TitleLine2En, b.TitleLine2Ar);
    public static string Lede(this Banner b) => Pick(b.LedeEn, b.LedeAr);
    public static string PrimaryCtaText(this Banner b) => Pick(b.PrimaryCtaTextEn, b.PrimaryCtaTextAr);
    public static string SecondaryCtaText(this Banner b) => Pick(b.SecondaryCtaTextEn, b.SecondaryCtaTextAr);
    public static string Label(this BannerStat s) => Pick(s.LabelEn, s.LabelAr);
    public static string Alt(this BannerImage i) => Pick(i.AltEn, i.AltAr);

    // ── Orders (line snapshot) ──────────────────────────────────────────────
    public static string Title(this OrderItem i) => Pick(i.TitleEn, i.TitleAr);
}
