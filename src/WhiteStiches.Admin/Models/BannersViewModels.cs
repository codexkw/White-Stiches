using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Models;

public class BannerIndexViewModel
{
    public PagedResult<BannerListItem> Banners { get; set; } = new();
}

/// <summary>One stat-counter row in the edit form (inputs named Stats[i].Value / .LabelEn / …).</summary>
public class BannerStatRow
{
    public string? Value { get; set; }
    public string? LabelEn { get; set; }
    public string? LabelAr { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>Bound by POST /banners/save.</summary>
public class BannerFormViewModel
{
    public int Id { get; set; }

    public string AdminLabel { get; set; } = string.Empty;

    public string? EyebrowEn { get; set; }
    public string? EyebrowAr { get; set; }

    public string TitleLine1En { get; set; } = string.Empty;
    public string? TitleLine1Ar { get; set; }

    public string? TitleLine2En { get; set; }
    public string? TitleLine2Ar { get; set; }
    public bool TitleLine2Italic { get; set; }

    public string? LedeEn { get; set; }
    public string? LedeAr { get; set; }

    public string? PrimaryCtaTextEn { get; set; }
    public string? PrimaryCtaTextAr { get; set; }
    public string? PrimaryCtaUrl { get; set; }

    public string? SecondaryCtaTextEn { get; set; }
    public string? SecondaryCtaTextAr { get; set; }
    public string? SecondaryCtaUrl { get; set; }

    public bool IsActive { get; set; }
    public bool ShowStats { get; set; }
    public int SortOrder { get; set; }

    public List<BannerStatRow> Stats { get; set; } = [];
}

/// <summary>A background medium already attached to the banner (admin media table).</summary>
public class BannerImageRow
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public MediaKind MediaKind { get; set; }
    public string? AltEn { get; set; }
    public string? AltAr { get; set; }
    public int SortOrder { get; set; }
}

public class BannerEditViewModel
{
    public BannerFormViewModel Form { get; set; } = new();
    public bool IsNew => Form.Id == 0;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public List<BannerImageRow> Images { get; set; } = [];
}
