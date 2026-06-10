using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Infrastructure.Services.Admin;

public class StaffAdminService(
    UserManager<ApplicationUser> userManager,
    WhiteStichesDbContext db) : IStaffAdminService
{
    public async Task<IReadOnlyList<StaffMember>> GetStaffAsync(CancellationToken ct = default)
    {
        var staffRoles = AppRoles.StaffRoles;

        var assignments = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                where r.Name != null && staffRoles.Contains(r.Name)
                select new { ur.UserId, RoleName = r.Name! })
            .AsNoTracking()
            .ToListAsync(ct);

        var rolesByUser = assignments
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.RoleName).ToList());

        var userIds = rolesByUser.Keys.ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Email)
            .ToListAsync(ct);

        return users.Select(u => Map(u, rolesByUser[u.Id])).ToList();
    }

    public async Task<StaffMember?> GetStaffMemberAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        var roles = await userManager.GetRolesAsync(user);
        return Map(user, roles);
    }

    public async Task<StaffOperationResult> CreateStaffAsync(string firstName, string lastName, string email,
        string password, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var staffRoles = NormalizeStaffRoles(roles);
        if (staffRoles.Count == 0)
        {
            return StaffOperationResult.Fail("Select at least one staff role.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return StaffOperationResult.Fail($"A user with email {email} already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsStaff = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return StaffOperationResult.Fail(createResult.Errors.Select(e => e.Description).ToArray());
        }

        var roleResult = await userManager.AddToRolesAsync(user, staffRoles);
        if (!roleResult.Succeeded)
        {
            return StaffOperationResult.Fail(roleResult.Errors.Select(e => e.Description).ToArray());
        }

        return StaffOperationResult.Success(user.Id);
    }

    public async Task<StaffOperationResult> SetRolesAsync(Guid userId, IReadOnlyList<string> roles,
        Guid actingUserId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return StaffOperationResult.Fail("Staff member not found.");

        var desired = NormalizeStaffRoles(roles);
        var current = (await userManager.GetRolesAsync(user))
            .Intersect(AppRoles.StaffRoles, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var removesSuperAdmin = current.Contains(AppRoles.SuperAdmin) && !desired.Contains(AppRoles.SuperAdmin);
        if (removesSuperAdmin && await CountSuperAdminsAsync() <= 1)
        {
            return StaffOperationResult.Fail("Cannot remove the SuperAdmin role from the last Super Admin.");
        }

        var toRemove = current.Except(desired, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = desired.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                return StaffOperationResult.Fail(removeResult.Errors.Select(e => e.Description).ToArray());
            }
        }

        if (toAdd.Count > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                return StaffOperationResult.Fail(addResult.Errors.Select(e => e.Description).ToArray());
            }
        }

        return StaffOperationResult.Success(user.Id);
    }

    public async Task<StaffOperationResult> SetLockAsync(Guid userId, bool locked, Guid actingUserId,
        CancellationToken ct = default)
    {
        if (locked && userId == actingUserId)
        {
            return StaffOperationResult.Fail("You cannot lock your own account.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return StaffOperationResult.Fail("Staff member not found.");

        if (locked)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.SuperAdmin) && await CountActiveSuperAdminsAsync() <= 1)
            {
                return StaffOperationResult.Fail("Cannot lock the last active Super Admin.");
            }

            await userManager.SetLockoutEnabledAsync(user, true);
            var result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            if (!result.Succeeded)
            {
                return StaffOperationResult.Fail(result.Errors.Select(e => e.Description).ToArray());
            }
        }
        else
        {
            var result = await userManager.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
            {
                return StaffOperationResult.Fail(result.Errors.Select(e => e.Description).ToArray());
            }

            await userManager.ResetAccessFailedCountAsync(user);
        }

        return StaffOperationResult.Success(user.Id);
    }

    public async Task<StaffOperationResult> ResetPasswordAsync(Guid userId, string newPassword,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return StaffOperationResult.Fail("Staff member not found.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return StaffOperationResult.Fail(result.Errors.Select(e => e.Description).ToArray());
        }

        return StaffOperationResult.Success(user.Id);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<int> CountSuperAdminsAsync() =>
        (await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin)).Count;

    private async Task<int> CountActiveSuperAdminsAsync()
    {
        var superAdmins = await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        return superAdmins.Count(u => !IsLockedOut(u));
    }

    private static bool IsLockedOut(ApplicationUser user) =>
        user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private static List<string> NormalizeStaffRoles(IReadOnlyList<string> roles) =>
        AppRoles.StaffRoles
            .Where(staffRole => roles.Contains(staffRole, StringComparer.OrdinalIgnoreCase))
            .ToList();

    private static StaffMember Map(ApplicationUser user, IEnumerable<string> roles) =>
        new(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            AppRoles.StaffRoles.Where(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList(),
            user.TwoFactorEnabled,
            IsLockedOut(user),
            user.LastLoginAtUtc);
}
