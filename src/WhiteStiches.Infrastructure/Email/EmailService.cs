using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Infrastructure.Email;

/// <summary>
/// High-level transactional email orchestration (Phase 1C-3): resolves the store name + language,
/// renders a branded bilingual template, and hands it to <see cref="IEmailSender"/>. Every method
/// is fully guarded — a mail failure logs but never propagates, so checkout / fulfilment / reset
/// flows are never broken by SMTP problems.
/// </summary>
public sealed class EmailService(
    IEmailSender sender,
    ISettingsService settings,
    IOptions<SmtpOptions> options,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task SendPasswordResetAsync(string toEmail, string? toName, string? languageCode, string resetLink,
        CancellationToken ct = default)
    {
        try
        {
            var lang = Norm(languageCode);
            var store = await StoreNameAsync(lang, ct);
            var (subject, html) = EmailTemplates.PasswordReset(store, toName, resetLink, lang);
            await sender.SendAsync(toEmail, toName, subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build/send password-reset email to {To}.", toEmail);
        }
    }

    public async Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(order.Email)) return;
            var lang = Norm(order.LanguageCode);
            var store = await StoreNameAsync(lang, ct);
            var (subject, html) = EmailTemplates.OrderConfirmation(store, order, lang);
            await sender.SendAsync(order.Email, $"{order.ShipFirstName} {order.ShipLastName}".Trim(), subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build/send order-confirmation email for {OrderNumber}.", order.OrderNumber);
        }
    }

    public async Task SendOrderShippedAsync(Order order, Shipment shipment, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(order.Email)) return;
            var lang = Norm(order.LanguageCode);
            var store = await StoreNameAsync(lang, ct);
            var (subject, html) = EmailTemplates.OrderShipped(store, order, shipment, lang);
            await sender.SendAsync(order.Email, $"{order.ShipFirstName} {order.ShipLastName}".Trim(), subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build/send shipment email for {OrderNumber}.", order.OrderNumber);
        }
    }

    private async Task<string> StoreNameAsync(string lang, CancellationToken ct)
    {
        var en = await settings.GetAsync(SettingKeys.StoreNameEn, ct);
        var ar = await settings.GetAsync(SettingKeys.StoreNameAr, ct);
        var name = lang == "ar" ? (string.IsNullOrWhiteSpace(ar) ? en : ar) : (string.IsNullOrWhiteSpace(en) ? ar : en);
        return string.IsNullOrWhiteSpace(name) ? _opts.FromName : name;
    }

    private static string Norm(string? lang) =>
        string.Equals(lang?.Trim(), "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
}
