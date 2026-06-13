using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace WhiteStiches.Infrastructure;

/// <summary>
/// Named rate-limit policy keys (NFR-SEC-02) plus the shared per-client-IP partitioner, used by both
/// apps' <c>AddRateLimiter</c> configuration and the <c>[EnableRateLimiting]</c> attributes on the
/// protected controllers. The <c>AddRateLimiter</c> call itself lives in each app's Program.cs — the
/// ASP.NET rate-limiting service extensions resolve only in the web-SDK projects, not this library.
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "ws-auth";
    public const string Checkout = "ws-checkout";
    public const string Search = "ws-search";

    /// <summary>
    /// One-minute fixed window keyed by the caller's IP. Accurate only when <c>UseForwardedHeaders</c>
    /// runs first behind the TLS proxy — otherwise every visitor collapses onto the proxy's IP.
    /// QueueLimit 0 → over-limit requests are rejected immediately, never queued.
    /// </summary>
    public static RateLimitPartition<string> FixedPerIp(HttpContext ctx, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}
