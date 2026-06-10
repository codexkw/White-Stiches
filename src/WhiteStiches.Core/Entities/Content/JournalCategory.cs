namespace WhiteStiches.Core.Entities.Content;

public class JournalCategory : BaseEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<JournalPost> Posts { get; set; } = [];
}
