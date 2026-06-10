namespace WhiteStiches.Core.Entities.Content;

/// <summary>Message submitted through the contact form (SF-STA-02).</summary>
public class ContactMessage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime? RepliedAtUtc { get; set; }
}
