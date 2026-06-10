using WhiteStiches.Core.Entities.Content;

namespace WhiteStiches.Web.Models.Content;

/// <summary>Journal landing page — featured hero, category chips, article grid and pagination.</summary>
public class JournalIndexViewModel
{
    /// <summary>Newest post on page 1 — rendered as the featured hero. Null on later pages or when there are no posts.</summary>
    public JournalPost? Featured { get; init; }

    /// <summary>Posts rendered in the grid (page items minus the featured hero on page 1).</summary>
    public IReadOnlyList<JournalPost> GridPosts { get; init; } = [];

    public IReadOnlyList<JournalCategory> Categories { get; init; } = [];

    public string? ActiveCategorySlug { get; init; }

    public int Page { get; init; } = 1;
    public int TotalPages { get; init; }

    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public bool IsEmpty => Featured is null && GridPosts.Count == 0;
}
