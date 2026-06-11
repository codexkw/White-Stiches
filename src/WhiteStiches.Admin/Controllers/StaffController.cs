using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Staff accounts and role assignment (AD-SET-02).</summary>
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.Admin)]
[Route("staff")]
public class StaffController(IStaffAdminService staff, IAuditService audit) : Controller
{
    private const int PageSize = 25;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Staff & Roles";
        ViewData["Nav"] = "staff";

        var all = await staff.GetStaffAsync(ct);
        return View(new StaffListViewModel { Members = all.ToPagedResult(page, PageSize) });
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        ViewData["Title"] = "Add staff member";
        ViewData["Nav"] = "staff";

        return View(new StaffNewViewModel
        {
            StaffRoles = AppRoles.StaffRoles,
            RoleDescriptions = AppRoles.Descriptions
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? firstName, string? lastName, string? email,
        string? password, string[]? roles, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)
            || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["Err"] = "First name, last name, email, and a temporary password are required.";
            return RedirectToAction(nameof(New));
        }

        var result = await staff.CreateStaffAsync(firstName.Trim(), lastName.Trim(), email.Trim(),
            password, roles ?? [], ct);

        if (!result.Ok)
        {
            TempData["Err"] = result.ErrorMessage;
            return RedirectToAction(nameof(New));
        }

        await audit.LogAsync("staff.create", CurrentUserId(), User.Identity?.Name,
            entityType: "StaffUser", entityId: result.UserId?.ToString(),
            after: new { email = email.Trim(), roles = roles ?? [] },
            ipAddress: ClientIp(), ct: ct);

        TempData["Ok"] = $"Staff member {email.Trim()} created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{userId:guid}/edit")]
    public async Task<IActionResult> Edit(Guid userId, CancellationToken ct)
    {
        var member = await staff.GetStaffMemberAsync(userId, ct);
        if (member is null)
        {
            TempData["Err"] = "Staff member not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit {member.FullName}";
        ViewData["Nav"] = "staff";

        return View(new StaffEditViewModel
        {
            Member = member,
            StaffRoles = AppRoles.StaffRoles,
            RoleDescriptions = AppRoles.Descriptions,
            IsSelf = userId == CurrentUserId()
        });
    }

    [HttpPost("{userId:guid}/roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRoles(Guid userId, string[]? roles, CancellationToken ct)
    {
        var before = await staff.GetStaffMemberAsync(userId, ct);
        var result = await staff.SetRolesAsync(userId, roles ?? [], CurrentUserId() ?? Guid.Empty, ct);

        if (!result.Ok)
        {
            TempData["Err"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { userId });
        }

        await audit.LogAsync("staff.roles.update", CurrentUserId(), User.Identity?.Name,
            entityType: "StaffUser", entityId: userId.ToString(),
            before: new { roles = before?.Roles ?? [] },
            after: new { roles = roles ?? [] },
            ipAddress: ClientIp(), ct: ct);

        TempData["Ok"] = "Roles updated.";
        return RedirectToAction(nameof(Edit), new { userId });
    }

    [HttpPost("{userId:guid}/lock")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Lock(Guid userId, CancellationToken ct) => SetLock(userId, true, ct);

    [HttpPost("{userId:guid}/unlock")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(Guid userId, CancellationToken ct) => SetLock(userId, false, ct);

    [HttpPost("{userId:guid}/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid userId, string? newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Err"] = "Enter a new password.";
            return RedirectToAction(nameof(Edit), new { userId });
        }

        var result = await staff.ResetPasswordAsync(userId, newPassword, ct);
        if (!result.Ok)
        {
            TempData["Err"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { userId });
        }

        await audit.LogAsync("staff.password.reset", CurrentUserId(), User.Identity?.Name,
            entityType: "StaffUser", entityId: userId.ToString(),
            ipAddress: ClientIp(), ct: ct);

        TempData["Ok"] = "Password reset.";
        return RedirectToAction(nameof(Edit), new { userId });
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IActionResult> SetLock(Guid userId, bool locked, CancellationToken ct)
    {
        var result = await staff.SetLockAsync(userId, locked, CurrentUserId() ?? Guid.Empty, ct);

        if (!result.Ok)
        {
            TempData["Err"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { userId });
        }

        await audit.LogAsync(locked ? "staff.lock" : "staff.unlock", CurrentUserId(), User.Identity?.Name,
            entityType: "StaffUser", entityId: userId.ToString(),
            ipAddress: ClientIp(), ct: ct);

        TempData["Ok"] = locked ? "Account locked." : "Account unlocked.";
        return RedirectToAction(nameof(Edit), new { userId });
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
