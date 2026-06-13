using System.Globalization;
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

    public Task SendOrderDeliveredAsync(Order order, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.OrderDelivered(store, order, lang), "delivered", ct);

    public Task SendOrderCancelledAsync(Order order, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.OrderCancelled(store, order, lang), "cancellation", ct);

    public Task SendOrderRefundedAsync(Order order, Refund refund, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.OrderRefunded(store, order, refund, lang), "refund", ct);

    public Task SendPaymentFailedAsync(Order order, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.PaymentFailed(store, order, RetryUrl(), lang), "payment-failed", ct);

    public Task SendReturnRequestedAsync(Order order, ReturnRequest request, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.ReturnRequested(store, order, request, lang), "return-requested", ct);

    public Task SendReturnApprovedAsync(Order order, ReturnRequest request, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.ReturnApproved(store, order, request, lang), "return-approved", ct);

    public Task SendReturnRejectedAsync(Order order, ReturnRequest request, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.ReturnRejected(store, order, request, lang), "return-rejected", ct);

    public Task SendReturnReceivedAsync(Order order, ReturnRequest request, CancellationToken ct = default) =>
        SendOrderEmailAsync(order, (store, lang) => EmailTemplates.ReturnReceived(store, order, request, lang), "return-received", ct);

    public Task SendNewOrderNotificationAsync(Order order, CancellationToken ct = default) =>
        SendAdminAlertAsync(
            $"New order {order.OrderNumber}",
            $"New paid order {order.OrderNumber}",
            new (string, string)[]
            {
                ("Order", order.OrderNumber),
                ("Customer", $"{order.ShipFirstName} {order.ShipLastName}".Trim()),
                ("Email", order.Email),
                ("Phone", order.Phone),
                ("Total", $"{order.Total.ToString("0.000", CultureInfo.InvariantCulture)} {order.Currency}"),
                ("Channel", order.Channel.ToString())
            },
            $"new-order {order.OrderNumber}", ct);

    public Task SendNewReturnNotificationAsync(Order order, ReturnRequest request, CancellationToken ct = default) =>
        SendAdminAlertAsync(
            $"New return {request.RmaNumber} to review",
            $"New return {request.RmaNumber} awaiting review",
            new (string, string)[]
            {
                ("Return", request.RmaNumber),
                ("Order", order.OrderNumber),
                ("Customer", $"{order.ShipFirstName} {order.ShipLastName}".Trim()),
                ("Email", order.Email)
            },
            $"new-return {request.RmaNumber}", ct);

    public Task SendChargeMismatchAlertAsync(Order order, string chargeId, decimal capturedAmount, CancellationToken ct = default) =>
        SendAdminAlertAsync(
            $"ACTION: charge/total mismatch on {order.OrderNumber}",
            $"Charge captured but order held — {order.OrderNumber}",
            new (string, string)[]
            {
                ("Order", order.OrderNumber),
                ("Captured at Tap", $"{capturedAmount.ToString("0.000", CultureInfo.InvariantCulture)} {order.Currency}"),
                ("Order total", $"{order.Total.ToString("0.000", CultureInfo.InvariantCulture)} {order.Currency}"),
                ("Tap charge", chargeId)
            },
            $"charge-mismatch {order.OrderNumber}", ct);

    // ── Shared senders ───────────────────────────────────────────────────────

    /// <summary>Build + send a customer email keyed off the order's language. Fully guarded.</summary>
    private async Task SendOrderEmailAsync(Order order, Func<string, string, (string Subject, string Html)> build,
        string ctx, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(order.Email)) return;
            var lang = Norm(order.LanguageCode);
            var store = await StoreNameAsync(lang, ct);
            var (subject, html) = build(store, lang);
            await sender.SendAsync(order.Email, $"{order.ShipFirstName} {order.ShipLastName}".Trim(), subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build/send {Ctx} email for {OrderNumber}.", ctx, order.OrderNumber);
        }
    }

    /// <summary>Send an English staff alert to Smtp:AdminNotifyEmail. No-op (logged at debug) when unset. Guarded.</summary>
    private async Task SendAdminAlertAsync(string subjectLine, string heading,
        IReadOnlyList<(string Key, string Value)> rows, string ctx, CancellationToken ct)
    {
        try
        {
            var to = _opts.AdminNotifyEmail;
            if (string.IsNullOrWhiteSpace(to)) return;
            var store = await StoreNameAsync("en", ct);
            var (subject, html) = EmailTemplates.AdminAlert(store, subjectLine, heading, rows);
            await sender.SendAsync(to, store, subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Ctx} staff alert.", ctx);
        }
    }

    private string? RetryUrl() =>
        string.IsNullOrWhiteSpace(_opts.BaseUrl) ? null : _opts.BaseUrl.TrimEnd('/') + "/cart";

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
