using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Admin.Controllers;

/// <summary>
/// Read-only audit trail (AD-SET-02). The fallback staff policy covers viewing,
/// so ReadOnlyAuditor can browse without extra attributes.
/// </summary>
[Route("audit")]
public class AuditController(IAuditService audit) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "action")] string? actionFilter,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        ViewData["Title"] = "Audit Log";
        ViewData["Nav"] = "audit";

        if (page < 1) page = 1;

        var entries = await audit.GetEntriesAsync(
            string.IsNullOrWhiteSpace(actionFilter) ? null : actionFilter.Trim(),
            userId: null, page: page, pageSize: 50, ct: ct);

        return View(new AuditListViewModel { Entries = entries, ActionFilter = actionFilter });
    }
}
