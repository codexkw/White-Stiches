using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using WhiteStiches.Admin;
using WhiteStiches.Infrastructure;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Infrastructure.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWhiteStichesLocalization();
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.AddWhiteStichesInfrastructure(builder.Configuration);
builder.Services.AddWhiteStichesAdminServices();

// QuestPDF Community licence (free for organisations under $1M annual revenue) — must be set
// once before any invoice PDF is generated, otherwise QuestPDF throws at render time.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Per-client-IP rate limiting on the staff sign-in surface (NFR-SEC-02). Over-limit → 429 + Retry-After.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPolicies.FixedPerIp(ctx, permitLimit: 10));
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        if (!context.HttpContext.Response.HasStarted)
            await context.HttpContext.Response.WriteAsync("Too many requests. Please wait a moment and try again.", token);
    };
});

// Honor the TLS proxy's forwarded client IP so the rate limiter sees the real visitor instead of
// the proxy's IP. KnownProxies/Networks are cleared (upstream IP unknown at build time) — keep the
// origin reachable only via the proxy at the infra layer to stop X-Forwarded-For spoofing.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Default-deny: every Admin endpoint requires a staff role unless explicitly [AllowAnonymous]
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.StaffRoles)
        .Build());

var app = builder.Build();

// Resolve the real client IP from the proxy first, so the rate limiter (below) partitions correctly.
app.UseForwardedHeaders();

// Baseline security headers on every response (NFR-SEC-01). 'unsafe-inline' covers the admin's inline
// scripts/styles + chart init; Google Fonts are allowlisted; images allow data:/blob: (rich-text
// editor upload previews) and https:. Everything else is same-origin, with object/base/frame-ancestors/
// form-action locked down (frame-ancestors 'none' blocks clickjacking of the back office).
// Cloudflare's auto-injected Web Analytics beacon (static.cloudflareinsights.com, which posts to
// cloudflareinsights.com) is allowlisted so the edge RUM script isn't CSP-blocked in the browser.
const string contentSecurityPolicy =
    "default-src 'self'; " +
    "base-uri 'self'; " +
    "object-src 'none'; " +
    "frame-ancestors 'none'; " +
    "form-action 'self'; " +
    "img-src 'self' data: blob: https:; " +
    "font-src 'self' https://fonts.gstatic.com; " +
    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
    "script-src 'self' 'unsafe-inline' https://static.cloudflareinsights.com; " +
    "connect-src 'self' https://cloudflareinsights.com";
app.UseWhiteStichesSecurityHeaders(contentSecurityPolicy);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Resolve English/Arabic per request (query → culture cookie → Accept-Language) — Phase 1E‑3.
app.UseRequestLocalization(WhiteStichesLocalization.BuildOptions());

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Static assets must be reachable by anonymous visitors (the login/error pages
// load their CSS before sign-in); otherwise the default-deny fallback policy
// below blocks /css/* and the login page renders unstyled.
app.MapStaticAssets().AllowAnonymous();

// Shared upload storage, served at /media (same path the storefront uses).
// UseStaticFiles is middleware (runs before authorization), so /media is public too.
app.UseWhiteStichesMedia(builder.Configuration, app.Environment);

// Seeded catalog images live in the Web app's wwwroot/assets; bridge them so
// product thumbnails render in the back office too (dev/sibling-deploy only).
var webAssets = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "WhiteStiches.Web", "wwwroot", "assets"));
if (Directory.Exists(webAssets))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webAssets),
        RequestPath = "/assets"
    });
}

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

// Surface the SMTP/email configuration at startup (the back office sends shipment notifications).
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
