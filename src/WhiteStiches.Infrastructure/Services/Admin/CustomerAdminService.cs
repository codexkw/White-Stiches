using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Infrastructure.Services.Admin;

public class CustomerAdminService(
    WhiteStichesDbContext db,
    UserManager<ApplicationUser> userManager) : ICustomerAdminService
{
    public async Task<PagedResult<CustomerSummary>> SearchAsync(string? search = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = CustomerQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u =>
                ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(s) ||
                (u.Email != null && u.Email.Contains(s)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var items = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new CustomerSummary(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email ?? string.Empty,
                u.PhoneNumber,
                u.CreatedAtUtc,
                db.Orders.Count(o => o.UserId == u.Id && !o.IsDraft),
                db.Orders
                    .Where(o => o.UserId == u.Id && !o.IsDraft && o.Status != OrderStatus.Cancelled)
                    .Sum(o => (decimal?)o.Total) ?? 0m,
                u.MarketingEmailOptIn,
                u.MarketingWhatsAppOptIn,
                u.LockoutEnd != null && u.LockoutEnd > now,
                u.LastLoginAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CustomerSummary> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<CustomerDetail?> GetDetailAsync(Guid userId,
        int ordersPage = 1, int ordersPageSize = 10, CancellationToken ct = default)
    {
        ordersPage = Math.Max(1, ordersPage);
        ordersPageSize = Math.Clamp(ordersPageSize, 1, 100);

        var user = await CustomerQuery().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        var addresses = await db.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Label)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        var ordersQuery = db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId && !o.IsDraft)
            .OrderByDescending(o => o.PlacedAtUtc ?? o.CreatedAtUtc);

        var ordersCount = await ordersQuery.CountAsync(ct);
        var orderItems = await ordersQuery
            .Skip((ordersPage - 1) * ordersPageSize)
            .Take(ordersPageSize)
            .ToListAsync(ct);

        var recentOrders = new PagedResult<Order>
        {
            Items = orderItems,
            TotalCount = ordersCount,
            Page = ordersPage,
            PageSize = ordersPageSize
        };

        var totalSpent = await db.Orders
            .Where(o => o.UserId == userId && !o.IsDraft && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var wishlistCount = await db.WishlistItems.CountAsync(w => w.UserId == userId, ct);

        var isLockedOut = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;

        return new CustomerDetail(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            user.CreatedAtUtc,
            ordersCount,
            totalSpent,
            user.MarketingEmailOptIn,
            user.MarketingWhatsAppOptIn,
            isLockedOut,
            user.LastLoginAtUtc,
            user.PreferredLanguage,
            addresses,
            recentOrders,
            wishlistCount);
    }

    public async Task<CustomerLockoutChange?> SetLockoutAsync(Guid userId, bool locked, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        // Staff lockout is the Staff module's concern — never via the customer directory.
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Intersect(AppRoles.StaffRoles).Any()) return null;

        var wasLockedOut = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;

        if (!user.LockoutEnabled)
        {
            var enable = await userManager.SetLockoutEnabledAsync(user, true);
            if (!enable.Succeeded) return null;
        }

        var result = await userManager.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null);
        if (!result.Succeeded) return null;

        return new CustomerLockoutChange(user.Id, user.Email, wasLockedOut, locked);
    }

    /// <summary>Customers = users holding no staff role (per AppRoles.StaffRoles).</summary>
    private IQueryable<ApplicationUser> CustomerQuery()
    {
        var staffRoles = AppRoles.StaffRoles;
        return db.Users.AsNoTracking()
            .Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && staffRoles.Contains(r.Name!))));
    }
}
