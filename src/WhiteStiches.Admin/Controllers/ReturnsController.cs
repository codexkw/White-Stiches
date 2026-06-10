using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Returns queue: review, approve/reject, receive (restock), refund (AD-ORD-10).</summary>
[Route("returns")]
public class ReturnsController(IReturnAdminService returns, IAuditService audit) : Controller
{
    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? CurrentUserName => User.Identity?.Name;

    // ------------------------------------------------------------- queue

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = null, int page = 1)
    {
        ViewData["Title"] = "Returns";
        ViewData["Nav"] = "returns";

        // No "status" param at all => default to the Pending work queue.
        // "status=" (All option) => no filter.
        ReturnStatus? filter = null;
        if (status is null)
        {
            filter = ReturnStatus.Pending;
        }
        else if (Enum.TryParse<ReturnStatus>(status, true, out var parsed))
        {
            filter = parsed;
        }

        var result = await returns.GetQueueAsync(filter, page < 1 ? 1 : page, 25);

        return View(new ReturnsQueueViewModel
        {
            Returns = result,
            StatusFilter = filter?.ToString() ?? string.Empty
        });
    }

    // ------------------------------------------------------------- detail

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var request = await returns.GetDetailAsync(id);
        if (request is null)
        {
            return NotFound();
        }

        ViewData["Title"] = $"Return {request.RmaNumber}";
        ViewData["Nav"] = "returns";

        var suggested = request.Items.Sum(i => (i.OrderItem?.UnitPrice ?? 0m) * i.Quantity);
        var alreadyRefunded = request.Order.Refunds
            .Where(r => r.Status == RefundStatus.Completed)
            .Sum(r => r.Amount);
        var returnEvents = request.Order.Events
            .Where(e => e.Kind.StartsWith("return", StringComparison.OrdinalIgnoreCase) || e.Kind == "refund")
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();

        return View(new ReturnDetailViewModel
        {
            Request = request,
            SuggestedRefund = suggested,
            AlreadyRefunded = alreadyRefunded,
            ReturnEvents = returnEvents
        });
    }

    // ------------------------------------------------------------- transitions

    [HttpPost("{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? staffNote)
    {
        var result = await returns.ApproveAsync(id, staffNote, CurrentUserId);
        await FinishAsync("return.approve", id, result, new { StaffNote = staffNote });
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? staffNote)
    {
        if (string.IsNullOrWhiteSpace(staffNote))
        {
            TempData["Err"] = "A staff note is required to reject a return.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var result = await returns.RejectAsync(id, staffNote, CurrentUserId);
        await FinishAsync("return.reject", id, result, new { StaffNote = staffNote });
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/receive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id, bool restock = false)
    {
        var result = await returns.ReceiveAsync(id, restock, CurrentUserId);
        await FinishAsync("return.receive", id, result, new { Restock = restock });
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/refund")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id, string? amount)
    {
        // Parse invariant so "12.500" works regardless of request culture (no-JS form post).
        if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            TempData["Err"] = "Enter a refund amount greater than zero (e.g. 12.500).";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var result = await returns.RefundAsync(id, value, CurrentUserId);
        await FinishAsync("return.refund", id, result, new { Amount = value });
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ------------------------------------------------------------- helpers

    /// <summary>Audit-log a successful transition and set the toast for POST-redirect-GET.</summary>
    private async Task FinishAsync(string action, int id, ReturnActionResult result, object extra)
    {
        if (!result.Success)
        {
            TempData["Err"] = result.Message;
            return;
        }

        await audit.LogAsync(action, CurrentUserId, CurrentUserName,
            entityType: "ReturnRequest", entityId: id.ToString(),
            before: new { Status = result.OldStatus },
            after: new { Status = result.NewStatus, result.RmaNumber, result.OrderId, Extra = extra });

        TempData["Ok"] = result.Message;
    }
}
