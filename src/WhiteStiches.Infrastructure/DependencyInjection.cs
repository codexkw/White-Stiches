using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Infrastructure.Services;

namespace WhiteStiches.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the DbContext, ASP.NET Identity, and all White Stitches services.
    /// Both the storefront (Web) and the back office (Admin) call this — they share
    /// one backend through these services; there is no API layer between them.
    /// </summary>
    public static IServiceCollection AddWhiteStichesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<WhiteStichesDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(WhiteStichesDbContext).Assembly.FullName)));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<WhiteStichesDbContext>()
            .AddDefaultTokenProviders();

        services.AddMemoryCache();

        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IMarketingService, MarketingService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
