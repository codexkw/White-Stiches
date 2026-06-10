using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using WhiteStiches.Infrastructure;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddWhiteStichesInfrastructure(builder.Configuration);
builder.Services.AddWhiteStichesAdminServices();

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

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
