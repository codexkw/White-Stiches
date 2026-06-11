namespace WhiteStiches.Infrastructure.Email;

/// <summary>
/// Low-level SMTP transport (Phase 1C-3). Sends one HTML message and returns <c>false</c> on any
/// failure rather than throwing, so callers never have to guard a mail send.
/// </summary>
public interface IEmailSender
{
    Task<bool> SendAsync(string toEmail, string? toName, string subject, string htmlBody,
        CancellationToken ct = default);
}
