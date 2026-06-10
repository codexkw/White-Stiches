namespace WhiteStiches.Core.Entities.Content;

/// <summary>Editorial journal article (SF-JRN-01/02).</summary>
public class JournalPost : BaseEntity
{
    public string Slug { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    public string? ExcerptEn { get; set; }
    public string? ExcerptAr { get; set; }

    /// <summary>Long-form body (HTML) with images and pull quotes.</summary>
    public string? BodyEn { get; set; }
    public string? BodyAr { get; set; }

    public int? JournalCategoryId { get; set; }
    public JournalCategory? Category { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }

    public string AuthorName { get; set; } = string.Empty;
    public string? HeroImageUrl { get; set; }

    public bool IsPublished { get; set; }
    public DateTime? PublishAtUtc { get; set; }

    public int? ReadingTimeMinutes { get; set; }
}
