using WhiteStiches.Core.Entities.Content;

namespace WhiteStiches.Web.Models.Content;

/// <summary>Journal article page — the post plus "read next" recommendations.</summary>
public class JournalPostViewModel
{
    public required JournalPost Post { get; init; }

    public IReadOnlyList<JournalPost> Related { get; init; } = [];
}
