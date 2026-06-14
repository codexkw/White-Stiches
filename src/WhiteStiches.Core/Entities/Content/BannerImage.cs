using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Content;

/// <summary>A banner background medium — a still image or a playable video clip (reuses <see cref="MediaKind"/>).</summary>
public class BannerImage : BaseEntity
{
    public int BannerId { get; set; }
    public Banner Banner { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public MediaKind MediaKind { get; set; } = MediaKind.Image;

    public string? AltEn { get; set; }
    public string? AltAr { get; set; }

    public int SortOrder { get; set; }
}
