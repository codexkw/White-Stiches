using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Settings;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Infrastructure.Data;

/// <summary>Idempotent startup seeding: roles, super admin, root categories, baseline settings.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WhiteStichesDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // ---- Roles (PRD Section 9) ----
        foreach (var (name, description) in AppRoles.Descriptions)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                await roleManager.CreateAsync(new ApplicationRole(name, description));
            }
        }

        // ---- Super admin ----
        var adminEmail = configuration["SeedAdmin:Email"] ?? "admin@whitestiches.kw";
        var adminPassword = configuration["SeedAdmin:Password"] ?? "ChangeMe@WS-2026!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Super",
                LastName = "Admin",
                IsStaff = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, AppRoles.SuperAdmin);
                logger.LogInformation("Seeded super admin {Email}", adminEmail);
            }
            else
            {
                logger.LogWarning("Failed to seed super admin: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        // ---- Root categories (SF-HOM-03) ----
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { NameEn = "Jackets", NameAr = "جاكيتات", Slug = "jackets", SortOrder = 1 },
                new Category { NameEn = "Dresses", NameAr = "فساتين", Slug = "dresses", SortOrder = 2 },
                new Category { NameEn = "Suits", NameAr = "بدلات", Slug = "suits", SortOrder = 3 },
                new Category { NameEn = "Tops", NameAr = "بلوزات", Slug = "tops", SortOrder = 4 });
            await db.SaveChangesAsync();
        }

        // ---- Baseline settings ----
        var defaults = new Dictionary<string, (string Value, string Group)>
        {
            [SettingKeys.StoreNameEn] = ("White Stitches", "store"),
            [SettingKeys.StoreNameAr] = ("وايت ستيتشز", "store"),
            [SettingKeys.ContactEmail] = ("hello@whitestiches.kw", "store"),
            [SettingKeys.FreeShippingThreshold] = ("50", "shipping"),
            [SettingKeys.StandardShippingRate] = ("0", "shipping"),
            [SettingKeys.ExpressShippingRate] = ("3.5", "shipping"),
            [SettingKeys.SameDayShippingRate] = ("5.0", "shipping"),
            [SettingKeys.GiftWrapFee] = ("3.5", "cart"),
            [SettingKeys.MaintenanceMode] = ("false", "store")
        };

        var existingKeys = await db.StoreSettings.Select(s => s.Key).ToListAsync();
        foreach (var (key, (value, group)) in defaults)
        {
            if (!existingKeys.Contains(key))
            {
                db.StoreSettings.Add(new StoreSetting { Key = key, Value = value, Group = group });
            }
        }

        await db.SaveChangesAsync();
    }
}
