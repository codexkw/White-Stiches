using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Customers directory + profile + lockout toggle (AD-CUS-01/02).</summary>
[Route("customers")]
[Authorize(Roles = ViewerRoles)]
public class CustomersController(ICustomerAdminService customers, IAuditService audit) : Controller
{
    /// <summary>Roles with customer visibility per PRD §9 (auditor is read-only).</summary>
    private const string ViewerRoles =
        AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.OperationsManager + "," +
        AppRoles.CustomerService + "," + AppRoles.ReadOnlyAuditor;

    private const string EditorRoles =
        AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.OperationsManager + "," +
        AppRoles.CustomerService;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q = null, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Customers";
        ViewData["Nav"] = "customers";

        var result = await customers.SearchAsync(q, page, 20, ct);
        return View(new CustomerListViewModel { Customers = result, Search = q });
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Detail(Guid userId, int page = 1, CancellationToken ct = default)
    {
        var detail = await customers.GetDetailAsync(userId, page, 10, ct);
        if (detail is null)
        {
            TempData["Err"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = string.IsNullOrWhiteSpace(detail.FullName) ? detail.Email : detail.FullName;
        ViewData["Nav"] = "customers";

        return View(new CustomerDetailViewModel { Customer = detail });
    }

    [HttpPost("{userId:guid}/lock")]
    [Authorize(Roles = EditorRoles)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Lock(Guid userId, CancellationToken ct = default) =>
        SetLockoutAsync(userId, locked: true, ct);

    [HttpPost("{userId:guid}/unlock")]
    [Authorize(Roles = EditorRoles)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(Guid userId, CancellationToken ct = default) =>
        SetLockoutAsync(userId, locked: false, ct);

    private async Task<IActionResult> SetLockoutAsync(Guid userId, bool locked, CancellationToken ct)
    {
        var change = await customers.SetLockoutAsync(userId, locked, ct);
        if (change is null)
        {
            TempData["Err"] = "Customer not found or cannot be changed from here.";
            return RedirectToAction(nameof(Index));
        }

        await audit.LogAsync(
            locked ? "customer.lock" : "customer.unlock",
            CurrentUserId(),
            User.Identity?.Name,
            "ApplicationUser",
            change.UserId.ToString(),
            before: new { lockedOut = change.WasLockedOut },
            after: new { lockedOut = change.IsLockedOut },
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct: ct);

        TempData["Ok"] = locked
            ? "Customer account locked. They can no longer sign in."
            : "Customer account unlocked.";

        return RedirectToAction(nameof(Detail), new { userId });
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
