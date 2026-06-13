using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace WhiteStiches.Infrastructure;

/// <summary>
/// Writes baseline security response headers (NFR-SEC-01) on every response in both apps.
/// They are set inside <see cref="HttpResponse.OnStarting"/> so they still land on responses
/// produced by the exception-handler / status-code re-execution pipeline. The Content-Security-Policy
/// is supplied per app because the storefront and back office differ slightly (e.g. the admin
/// rich-text editor needs blob: image previews).
/// </summary>
public static class SecurityHeadersSetup
{
    public static IApplicationBuilder UseWhiteStichesSecurityHeaders(
        this IApplicationBuilder app, string contentSecurityPolicy)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";                       // legacy backstop for frame-ancestors
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), browsing-topics=()";
                headers["Content-Security-Policy"] = contentSecurityPolicy;
                return Task.CompletedTask;
            });

            await next();
        });
    }
}
