using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Content;

/// <summary>
/// A homepage hero banner — bilingual copy, two CTAs, a video/image background (<see cref="Images"/>)
/// and optional stat counters (<see cref="Stats"/>). The storefront shows the single active banner
/// with the highest <see cref="SortOrder"/>; others can be staged as drafts (<see cref="IsActive"/> = false).
/// </summary>
public class Banner : BaseEntity
{
    /// <summary>Internal name shown only in the admin list so staff can tell drafts apart.</summary>
    public string AdminLabel { get; set; } = string.Empty;

    // ── Copy (bilingual) ──────────────────────────────────────────────────
    public string? EyebrowEn { get; set; }
    public string? EyebrowAr { get; set; }

    public string TitleLine1En { get; set; } = string.Empty;
    public string? TitleLine1Ar { get; set; }

    public string? TitleLine2En { get; set; }
    public string? TitleLine2Ar { get; set; }

    /// <summary>Render the second title line in the display italic (matches the original hero).</summary>
    public bool TitleLine2Italic { get; set; } = true;

    public string? LedeEn { get; set; }
    public string? LedeAr { get; set; }

    // ── Calls to action ───────────────────────────────────────────────────
    public string? PrimaryCtaTextEn { get; set; }
    public string? PrimaryCtaTextAr { get; set; }
    public string? PrimaryCtaUrl { get; set; }

    public string? SecondaryCtaTextEn { get; set; }
    public string? SecondaryCtaTextAr { get; set; }
    public string? SecondaryCtaUrl { get; set; }

    // ── Visibility & ordering ─────────────────────────────────────────────
    /// <summary>Draft toggle: false hides the banner from the storefront.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Hide/show the whole stat-counter block.</summary>
    public bool ShowStats { get; set; } = true;

    /// <summary>Higher wins when several banners are active (the storefront shows exactly one).</summary>
    public int SortOrder { get; set; }

    public ICollection<BannerImage> Images { get; set; } = [];
    public ICollection<BannerStat> Stats { get; set; } = [];
}
