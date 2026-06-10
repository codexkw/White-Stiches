using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models.Payments;

namespace WhiteStiches.Infrastructure.Payments;

/// <summary>
/// Tap Payments v2 client (https://developers.tap.company). Hosted redirect flow:
/// POST /charges with source.id="src_all" + redirect.url returns a transaction.url to
/// redirect the customer to; the browser returns with ?tap_id=, which we GET /charges/{id}
/// to confirm (status CAPTURED). Webhooks carry an HMAC-SHA256 "hashstring" header.
/// All amounts are decimal major units (KWD uses 3 decimals — never minor units).
/// </summary>
public class TapPaymentService(HttpClient http, IOptions<TapOptions> options, ILogger<TapPaymentService> logger)
    : IPaymentGateway
{
    private readonly TapOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        // Property names are already the exact snake_case Tap expects; don't rename them.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new PaymentChargeResult { Success = false, Error = "Payment gateway is not configured." };

        var amount = RoundForCurrency(request.Amount, request.Currency);
        var phone = NormalizePhone(request.CustomerPhoneNumber);

        var payload = new Dictionary<string, object?>
        {
            ["amount"] = amount,
            ["currency"] = request.Currency,
            ["customer_initiated"] = true,
            ["threeDSecure"] = true,
            ["save_card"] = false,
            ["description"] = request.Description ?? $"Order {request.OrderNumber}",
            ["reference"] = new { transaction = request.OrderNumber, order = request.OrderNumber },
            ["receipt"] = new { email = false, sms = false },
            ["source"] = new { id = "src_all" },
            ["redirect"] = new { url = request.RedirectUrl },
            ["customer"] = new Dictionary<string, object?>
            {
                ["first_name"] = string.IsNullOrWhiteSpace(request.CustomerFirstName) ? "Customer" : request.CustomerFirstName.Trim(),
                ["last_name"] = string.IsNullOrWhiteSpace(request.CustomerLastName) ? null : request.CustomerLastName!.Trim(),
                ["email"] = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail!.Trim(),
                ["phone"] = phone is null ? null : new { country_code = request.CustomerPhoneCountryCode, number = phone }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.WebhookUrl))
            payload["post"] = new { url = request.WebhookUrl };
        if (!string.IsNullOrWhiteSpace(_options.MerchantId))
            payload["merchant"] = new { id = _options.MerchantId };

        try
        {
            using var resp = await http.PostAsJsonAsync("charges/", payload, JsonOpts, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("Tap create-charge failed ({Status}): {Body}", (int)resp.StatusCode, body);
                return new PaymentChargeResult { Success = false, RawJson = body, Error = $"Gateway returned {(int)resp.StatusCode}." };
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var id = GetString(root, "id");

            string? url = null;
            if (root.TryGetProperty("transaction", out var txn) && txn.ValueKind == JsonValueKind.Object)
                url = GetString(txn, "url");

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url))
            {
                logger.LogError("Tap create-charge response missing id/transaction.url: {Body}", body);
                return new PaymentChargeResult { Success = false, RawJson = body, Error = "Gateway response missing the redirect URL." };
            }

            return new PaymentChargeResult { Success = true, ChargeId = id, HostedPaymentUrl = url, RawJson = body };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tap create-charge threw.");
            return new PaymentChargeResult { Success = false, Error = "Could not reach the payment gateway." };
        }
    }

    public async Task<PaymentChargeStatus> RetrieveChargeAsync(string chargeId, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new PaymentChargeStatus { ChargeId = chargeId, State = GatewayChargeState.Failed, RawStatus = "NOT_CONFIGURED" };

        try
        {
            using var resp = await http.GetAsync($"charges/{Uri.EscapeDataString(chargeId)}", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("Tap retrieve-charge failed ({Status}): {Body}", (int)resp.StatusCode, body);
                return new PaymentChargeStatus { ChargeId = chargeId, State = GatewayChargeState.Failed, RawStatus = "ERROR", RawJson = body };
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var status = GetString(root, "status") ?? string.Empty;
            var amount = root.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number ? amt.GetDecimal() : 0m;

            return new PaymentChargeStatus
            {
                ChargeId = chargeId,
                State = MapStatus(status),
                RawStatus = status,
                Amount = amount,
                RawJson = body
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tap retrieve-charge threw.");
            return new PaymentChargeStatus { ChargeId = chargeId, State = GatewayChargeState.Failed, RawStatus = "EXCEPTION" };
        }
    }

    public async Task<PaymentRefundResult> CreateRefundAsync(PaymentRefundRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new PaymentRefundResult { Success = false, Error = "Payment gateway is not configured." };

        var amount = RoundForCurrency(request.Amount, request.Currency);

        var payload = new Dictionary<string, object?>
        {
            ["charge_id"] = request.ChargeId,
            ["amount"] = amount,
            ["currency"] = request.Currency,
            ["reason"] = MapRefundReason(request.Reason),
            ["description"] = request.Description ?? "Refund"
        };

        try
        {
            using var resp = await http.PostAsJsonAsync("refunds/", payload, JsonOpts, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("Tap refund failed ({Status}): {Body}", (int)resp.StatusCode, body);
                return new PaymentRefundResult { Success = false, RawJson = body, Error = $"Gateway returned {(int)resp.StatusCode}." };
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new PaymentRefundResult
            {
                Success = true,
                RefundId = GetString(root, "id"),
                RawStatus = GetString(root, "status"),
                RawJson = body
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tap refund threw.");
            return new PaymentRefundResult { Success = false, Error = "Could not reach the payment gateway." };
        }
    }

    public PaymentWebhookResult ParseAndVerifyWebhook(string rawBody, string? signatureHeader)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var id = GetString(root, "id");
            var status = GetString(root, "status") ?? string.Empty;
            var currency = GetString(root, "currency") ?? "KWD";
            var amount = root.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number ? amt.GetDecimal() : 0m;

            string gatewayRef = string.Empty, paymentRef = string.Empty;
            if (root.TryGetProperty("reference", out var refEl) && refEl.ValueKind == JsonValueKind.Object)
            {
                gatewayRef = GetString(refEl, "gateway") ?? string.Empty;
                paymentRef = GetString(refEl, "payment") ?? string.Empty;
            }

            var created = string.Empty;
            if (root.TryGetProperty("transaction", out var txn) && txn.ValueKind == JsonValueKind.Object)
                created = RawScalar(txn, "created");

            // Exact Tap charge concatenation (no separators); amount rounded to the currency's decimals.
            var amountStr = amount.ToString("F" + CurrencyDecimals(currency), CultureInfo.InvariantCulture);
            var toSign = $"x_id{id}x_amount{amountStr}x_currency{currency}"
                       + $"x_gateway_reference{gatewayRef}x_payment_reference{paymentRef}"
                       + $"x_status{status}x_created{created}";

            var expected = ComputeHmacHex(toSign, _options.SecretKey);
            var isValid = IsConfigured
                && !string.IsNullOrWhiteSpace(signatureHeader)
                && FixedTimeEquals(expected, signatureHeader.Trim());

            if (!isValid)
                logger.LogWarning("Tap webhook signature mismatch for charge {Id} (status {Status}).", id, status);

            return new PaymentWebhookResult
            {
                IsValid = isValid,
                ChargeId = id,
                State = MapStatus(status),
                RawStatus = status,
                Amount = amount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tap webhook parse failed.");
            return new PaymentWebhookResult { IsValid = false };
        }
    }

    // ---------------------------------------------------------------- helpers

    private static GatewayChargeState MapStatus(string status) => status.ToUpperInvariant() switch
    {
        "CAPTURED" => GatewayChargeState.Captured,
        "INITIATED" or "PENDING" or "IN_PROGRESS" => GatewayChargeState.Pending,
        _ => GatewayChargeState.Failed // ABANDONED, CANCELLED, FAILED, DECLINED, RESTRICTED, VOID, TIMEDOUT, UNKNOWN
    };

    private static string MapRefundReason(string reason) => reason switch
    {
        "duplicate" or "fraudulent" or "requested_by_customer" => reason,
        _ => "requested_by_customer"
    };

    private static decimal RoundForCurrency(decimal amount, string currency) =>
        Math.Round(amount, CurrencyDecimals(currency), MidpointRounding.AwayFromZero);

    private static int CurrencyDecimals(string currency) => currency.ToUpperInvariant() switch
    {
        "KWD" or "BHD" or "OMR" or "JOD" or "TND" or "LYD" => 3,
        "JPY" or "KRW" => 0,
        _ => 2
    };

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length > 8 && digits.StartsWith("965")) digits = digits[3..];
        return digits.Length == 0 ? null : digits;
    }

    private static string ComputeHmacHex(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind is JsonValueKind.Null ? null : v.ToString()
            : null;

    /// <summary>Reads a scalar exactly as Tap sent it (string content without quotes, or raw number text).</summary>
    private static string RawScalar(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return string.Empty;
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.GetRawText();
    }
}
