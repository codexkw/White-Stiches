namespace WhiteStiches.Core.Entities.Marketing;

/// <summary>Newsletter signup from the home page band or maintenance page (SF-HOM-05).</summary>
public class NewsletterSubscriber : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    public bool WhatsAppOptIn { get; set; }

    public string LanguageCode { get; set; } = "en";

    /// <summary>Capture source: "home", "maintenance", "checkout", "footer".</summary>
    public string? Source { get; set; }

    public DateTime? UnsubscribedAtUtc { get; set; }
}
