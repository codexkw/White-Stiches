using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WhiteStiches.Infrastructure.Email;

/// <summary>
/// SMTP transport over the built-in <see cref="SmtpClient"/> (no third-party dependency, matching
/// the codebase's lean approach). Port 587 + <c>UseSsl=true</c> performs STARTTLS, which is what
/// Mailgun (smtp.mailgun.org) expects. Never throws — failures are logged and reported as false.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task<bool> SendAsync(string toEmail, string? toName, string subject, string htmlBody,
        CancellationToken ct = default)
    {
        if (!_opts.IsConfigured)
        {
            logger.LogWarning("SMTP not configured — skipping email '{Subject}' to {To}.", subject, toEmail);
            return false;
        }
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            logger.LogWarning("SMTP: empty recipient for email '{Subject}'.", subject);
            return false;
        }

        try
        {
            using var client = new SmtpClient(_opts.Host, _opts.Port)
            {
                EnableSsl = _opts.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(_opts.Username, _opts.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_opts.FromEmail, _opts.FromName, Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(string.IsNullOrWhiteSpace(toName)
                ? new MailAddress(toEmail)
                : new MailAddress(toEmail, toName, Encoding.UTF8));

            await client.SendMailAsync(message, ct);
            logger.LogInformation("SMTP: sent '{Subject}' to {To}.", subject, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP: failed to send '{Subject}' to {To}.", subject, toEmail);
            return false;
        }
    }
}
