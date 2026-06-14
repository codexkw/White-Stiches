namespace WhiteStiches.Core.Entities.Content;

/// <summary>One hero stat counter — a value (e.g. "28") with a bilingual label, individually hideable.</summary>
public class BannerStat : BaseEntity
{
    public int BannerId { get; set; }
    public Banner Banner { get; set; } = null!;

    public string Value { get; set; } = string.Empty;
    public string? LabelEn { get; set; }
    public string? LabelAr { get; set; }

    /// <summary>Hide this single counter without deleting it.</summary>
    public bool IsVisible { get; set; } = true;

    public int SortOrder { get; set; }
}
