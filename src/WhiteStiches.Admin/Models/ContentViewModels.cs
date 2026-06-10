using System.ComponentModel.DataAnnotations;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

/// <summary>Create/edit form for a static page (AD-CNT-01).</summary>
public class PageEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "English title is required.")]
    public string TitleEn { get; set; } = string.Empty;

    public string? TitleAr { get; set; }

    /// <summary>Leave blank to auto-generate from the EN title.</summary>
    public string? Slug { get; set; }

    public string? BodyEn { get; set; }
    public string? BodyAr { get; set; }

    public string? SeoTitleEn { get; set; }
    public string? SeoTitleAr { get; set; }
    public string? SeoDescriptionEn { get; set; }
    public string? SeoDescriptionAr { get; set; }

    public bool IsPublished { get; set; } = true;
}

/// <summary>Journal admin list (incl. drafts) with search filter (AD-CNT-02).</summary>
public class JournalListViewModel
{
    public PagedResult<JournalPost> Posts { get; set; } = new();
    public string? Search { get; set; }
}

/// <summary>Create/edit form for a journal post (AD-CNT-02).</summary>
public class JournalEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "English title is required.")]
    public string TitleEn { get; set; } = string.Empty;

    public string? TitleAr { get; set; }

    /// <summary>Leave blank to auto-generate from the EN title.</summary>
    public string? Slug { get; set; }

    public string? ExcerptEn { get; set; }
    public string? ExcerptAr { get; set; }

    public string? BodyEn { get; set; }
    public string? BodyAr { get; set; }

    public int? JournalCategoryId { get; set; }

    /// <summary>When filled, a category with this EN name is created and assigned.</summary>
    public string? NewCategoryName { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }

    public string? AuthorName { get; set; }

    /// <summary>Current stored hero path (kept when no new file is uploaded).</summary>
    public string? HeroImageUrl { get; set; }

    /// <summary>Optional replacement upload; form must also accept an empty file input.</summary>
    public IFormFile? HeroImage { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>Optional publish date, UTC. Accepts "yyyy-MM-dd HH:mm" or "yyyy-MM-ddTHH:mm".</summary>
    public string? PublishAtUtc { get; set; }

    public int? ReadingTimeMinutes { get; set; }

    public IReadOnlyList<JournalCategory> Categories { get; set; } = [];
}

/// <summary>Contact inbox list; unread-only filter defaults to on (AD-CNT, inbox).</summary>
public class InboxListViewModel
{
    public PagedResult<ContactMessage> Messages { get; set; } = new();
    public bool UnreadOnly { get; set; } = true;
}
