using Microsoft.AspNetCore.Identity;

namespace WhiteStiches.Infrastructure.Identity;

/// <summary>Unified identity for customers and staff. Staff membership is expressed through roles.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Persisted language preference — wins over the cookie for logged-in users (LOC-02).</summary>
    public string PreferredLanguage { get; set; } = "en";

    public string PreferredCurrency { get; set; } = "KWD";

    public bool MarketingEmailOptIn { get; set; }
    public bool MarketingSmsOptIn { get; set; }
    public bool MarketingWhatsAppOptIn { get; set; }

    public bool IsStaff { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
