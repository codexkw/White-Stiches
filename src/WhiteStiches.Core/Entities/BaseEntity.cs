namespace WhiteStiches.Core.Entities;

/// <summary>Base for all persisted entities. Int identity keys, UTC audit timestamps.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
