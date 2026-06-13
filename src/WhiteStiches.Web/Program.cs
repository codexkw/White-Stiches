using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using WhiteStiches.Infrastructure;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Localization;
using WhiteStiches.Web;
using WhiteStiches.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWhiteStichesLocalization();
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.AddWhiteStichesInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WhiteStiches.Web.Infrastructure.ICurrentCartAccessor, WhiteStiches.Web.Infrastructure.CurrentCartAccessor>();

// Per-client-IP rate limiting on auth / checkout / search (NFR-SEC-02). Over-limit → 429 + Retry-After.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPolicies.FixedPerIp(ctx, permitLimit: 10));
    options.AddPolicy(RateLimitPolicies.Checkout, ctx => RateLimitPolicies.FixedPerIp(ctx, permitLimit: 30));
    options.AddPolicy(RateLimitPolicies.Search, ctx => RateLimitPolicies.FixedPerIp(ctx, permitLimit: 60));
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        if (!context.HttpContext.Response.HasStarted)
            await context.HttpContext.Response.WriteAsync("Too many requests. Please wait a moment and try again.", token);
    };
});

// Honor the TLS proxy's forwarded client IP so the rate limiter partitions by the real visitor
// instead of collapsing everyone onto the proxy's IP (which would throttle all users together).
// KnownProxies/Networks are cleared because the upstream IP isn't known at build time — keep the
// origin reachable only via the proxy at the infra layer to stop X-Forwarded-For spoofing.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Resolve the real client IP from the proxy first, so the rate limiter (below) partitions correctly.
app.UseForwardedHeaders();

// Baseline security headers on every response (NFR-SEC-01). 'unsafe-inline' is unavoidable for the
// inline <script>/<style> blocks and style="" attributes carried over from the static design; the
// Google Fonts origins are allowlisted; images may be data: URIs or any https source (rich-text
// bodies). Everything else is same-origin, with object/base/frame-ancestors/form-action locked down.
// Cloudflare's auto-injected Web Analytics beacon (static.cloudflareinsights.com, which posts to
// cloudflareinsights.com) is allowlisted so the edge RUM script isn't CSP-blocked in the browser.
const string contentSecurityPolicy =
    "default-src 'self'; " +
    "base-uri 'self'; " +
    "object-src 'none'; " +
    "frame-ancestors 'none'; " +
    "form-action 'self'; " +
    "img-src 'self' data: https:; " +
    "font-src 'self' https://fonts.gstatic.com; " +
    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
    "script-src 'self' 'unsafe-inline' https://static.cloudflareinsights.com; " +
    "connect-src 'self' https://cloudflareinsights.com";
app.UseWhiteStichesSecurityHeaders(contentSecurityPolicy);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Render the branded 404 page for unmatched routes and 404 results
app.UseStatusCodePagesWithReExecute("/not-found");

// Resolve English/Arabic per request (query → culture cookie → Accept-Language) — Phase 1E‑3.
app.UseRequestLocalization(WhiteStichesLocalization.BuildOptions());

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Redirect public traffic to /maintenance when the store.maintenance_mode setting is on
// (after auth so signed-in staff bypass the gate). Admin app is never gated.
app.UseMiddleware<MaintenanceMiddleware>();

app.MapStaticAssets();

// Shared upload storage (product images etc.) written by Admin, served here at /media
app.UseWhiteStichesMedia(builder.Configuration, app.Environment);

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Surface the Tap payment configuration at startup so a deployment can be verified from the logs:
// an empty SecretKey means checkout uses the Development-only manual fallback (and is unavailable in
// production), and an empty PublicBaseUrl means callback/webhook URLs fall back to the request scheme.
var tapConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Tap:SecretKey"]);
var tapPublicBase = builder.Configuration["Tap:PublicBaseUrl"];
app.Logger.LogInformation(
    "Tap payments: {State}. PublicBaseUrl: {PublicBase}",
    tapConfigured
        ? "CONFIGURED"
        : "NOT configured — checkout falls back to the manual flow in Development, and is unavailable in other environments",
    string.IsNullOrWhiteSpace(tapPublicBase)
        ? "(not set — callback/webhook URLs use the incoming request scheme + host)"
        : tapPublicBase);

// Surface the SMTP/email configuration at startup. NOT configured means transactional mail
// (password reset, order confirmation, shipment) is silently skipped (logged as a warning per send).
var smtpHost = builder.Configuration["Smtp:Host"];
var smtpUser = builder.Configuration["Smtp:Username"];
app.Logger.LogInformation(
    "SMTP email: {State}",
    !string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser)
        ? $"CONFIGURED ({smtpHost})"
        : "NOT configured — transactional email is skipped");

// Idempotent seed: roles, super admin, root categories, baseline settings
try
{
    await DbSeeder.SeedAsync(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database seeding failed — continuing startup. Apply migrations and restart.");
}

app.Run();
