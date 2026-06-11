namespace WhiteStiches.Infrastructure.Email;

/// <summary>
/// Strongly-typed <c>Smtp</c> configuration section (Phase 1C-3), mirroring the TapOptions pattern.
/// Bound from appsettings in <c>AddWhiteStichesInfrastructure</c>.
/// </summary>
public class SmtpOptions
{
    /// <summary>Master switch — set false to disable all outbound mail without removing config.</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = string.Empty;

    /// <summary>587 (STARTTLS, recommended for Mailgun) or 465 (implicit SSL) or 2525/25.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Use TLS. On 587 the built-in client upgrades via STARTTLS.</summary>
    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Envelope From — must be on the authenticated sending domain to pass SPF/DKIM.</summary>
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "White Stitches";

    /// <summary>Public storefront origin (https) used to build absolute links inside emails.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>True only when enough is set to actually send (host + credentials + From).</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromEmail)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password);
}
