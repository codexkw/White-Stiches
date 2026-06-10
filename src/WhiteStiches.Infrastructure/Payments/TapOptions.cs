namespace WhiteStiches.Infrastructure.Payments;

/// <summary>
/// Bound from the "Tap" configuration section. The environment is chosen by the key
/// prefix — sk_test_ (sandbox) vs sk_live_ (production); the base URL is the same host.
/// Keep <see cref="SecretKey"/> out of source control (user-secrets / env var).
/// </summary>
public class TapOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.tap.company/v2/";

    /// <summary>Secret API key (sk_test_… or sk_live_…). Also the HMAC key for webhook signatures.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Optional Tap merchant id; sent on charges when present.</summary>
    public string? MerchantId { get; set; }

    /// <summary>
    /// Public origin (e.g. "https://shop.example.com") used to build the redirect/webhook URLs.
    /// Set this in production so Tap always receives an https callback even behind a TLS-terminating
    /// proxy. When empty, callback URLs fall back to the current request scheme/host.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
