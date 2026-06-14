namespace WhiteStiches.Core.Models.Admin;

/// <summary>Row DTO for the back-office banners list (Wave 4 #1 — homepage hero CMS).</summary>
public record BannerListItem(
    int Id,
    string AdminLabel,
    string TitleLine1En,
    bool IsActive,
    int SortOrder,
    int MediaCount,
    int StatCount,
    DateTime UpdatedAtUtc);
