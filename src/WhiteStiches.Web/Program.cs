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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

var app = builder.Build();

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
