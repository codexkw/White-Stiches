using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services.Admin;

/// <summary>Returns queue transitions (AD-ORD-10). Reuses IOrderService for the shared status + event plumbing.</summary>
public class ReturnAdminService(WhiteStichesDbContext db, IOrderService orders, IEmailService emailService) : IReturnAdminService
{
    public Task<PagedResult<ReturnRequest>> GetQueueAsync(ReturnStatus? status,
        int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        orders.GetReturnRequestsAsync(status, page, pageSize, ct);

    public Task<ReturnRequest?> GetDetailAsync(int id, CancellationToken ct = default) =>
        db.ReturnRequests
            .AsNoTracking()
            .Include(r => r.Order).ThenInclude(o => o.Refunds)
            .Include(r => r.Order).ThenInclude(o => o.Events.OrderByDescending(e => e.CreatedAtUtc))
            .Include(r => r.Items).ThenInclude(i => i.OrderItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<ReturnActionResult> ApproveAsync(int id, string? staffNote, Guid? staffUserId, CancellationToken ct = default)
    {
        var request = await db.ReturnRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return ReturnActionResult.Fail($"Return request #{id} not found.");
        if (request.Status != ReturnStatus.Pending)
        {
            return ReturnActionResult.Fail(
                $"Return {request.RmaNumber} is {request.Status} — only Pending requests can be approved.");
        }

        var note = string.IsNullOrWhiteSpace(staffNote) ? null : staffNote.Trim();
        await orders.UpdateReturnStatusAsync(id, ReturnStatus.Approved, note, staffUserId, ct);

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is not null)
        {
            request.StaffNote = note;
            await emailService.SendReturnApprovedAsync(order, request, ct);
        }

        return new ReturnActionResult(true, $"Return {request.RmaNumber} approved.",
            request.OrderId, request.RmaNumber,
            nameof(ReturnStatus.Pending), nameof(ReturnStatus.Approved));
    }

    public async Task<ReturnActionResult> RejectAsync(int id, string staffNote, Guid? staffUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(staffNote))
        {
            return ReturnActionResult.Fail("A staff note is required to reject a return.");
        }

        var request = await db.ReturnRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return ReturnActionResult.Fail($"Return request #{id} not found.");
        if (request.Status != ReturnStatus.Pending)
        {
            return ReturnActionResult.Fail(
                $"Return {request.RmaNumber} is {request.Status} — only Pending requests can be rejected.");
        }

        await orders.UpdateReturnStatusAsync(id, ReturnStatus.Rejected, staffNote.Trim(), staffUserId, ct);

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is not null)
        {
            request.StaffNote = staffNote.Trim();
            await emailService.SendReturnRejectedAsync(order, request, ct);
        }

        return new ReturnActionResult(true, $"Return {request.RmaNumber} rejected.",
            request.OrderId, request.RmaNumber,
            nameof(ReturnStatus.Pending), nameof(ReturnStatus.Rejected));
    }

    public async Task<ReturnActionResult> ReceiveAsync(int id, bool restock, Guid? staffUserId, CancellationToken ct = default)
    {
        var request = await db.ReturnRequests
            .Include(r => r.Items).ThenInclude(i => i.OrderItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request is null) return ReturnActionResult.Fail($"Return request #{id} not found.");
        if (request.Status != ReturnStatus.Approved)
        {
            return ReturnActionResult.Fail(
                $"Return {request.RmaNumber} is {request.Status} — only Approved returns can be received.");
        }

        var restockedUnits = 0;
        var skippedItems = 0;

        if (restock)
        {
            foreach (var item in request.Items)
            {
                var variantId = item.OrderItem?.ProductVariantId;
                if (variantId is int vid)
                {
                    var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == vid, ct);
                    if (variant is not null)
                    {
                        variant.StockQuantity += item.Quantity;
                        item.Restocked = true;
                        restockedUnits += item.Quantity;
                        continue;
                    }
                }

                // Variant deleted (or never linked) — skip silently, note it on the event.
                skippedItems++;
            }
        }

        request.Status = ReturnStatus.Received;
        request.ProcessedByUserId = staffUserId;

        var description = restock
            ? $"Return {request.RmaNumber} received. Restocked {restockedUnits} unit(s)."
              + (skippedItems > 0 ? $" {skippedItems} item(s) skipped — variant no longer exists." : string.Empty)
            : $"Return {request.RmaNumber} received without restock.";

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = request.OrderId,
            Kind = "return",
            Description = description,
            AuthorUserId = staffUserId
        });

        await db.SaveChangesAsync(ct);

        return new ReturnActionResult(true, description,
            request.OrderId, request.RmaNumber,
            nameof(ReturnStatus.Approved), nameof(ReturnStatus.Received));
    }

    public async Task<ReturnActionResult> RefundAsync(int id, decimal amount, Guid? staffUserId, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return ReturnActionResult.Fail("Refund amount must be greater than zero.");
        }

        var request = await db.ReturnRequests
            .Include(r => r.Order).ThenInclude(o => o.Refunds)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request is null) return ReturnActionResult.Fail($"Return request #{id} not found.");
        if (request.Status != ReturnStatus.Received)
        {
            return ReturnActionResult.Fail(
                $"Return {request.RmaNumber} is {request.Status} — only Received returns can be refunded.");
        }

        var order = request.Order;

        // Snapshot before EF relationship fix-up adds the new refund to order.Refunds.
        var previouslyRefunded = order.Refunds
            .Where(r => r.Status == RefundStatus.Completed)
            .Sum(r => r.Amount);

        var refund = new Refund
        {
            OrderId = order.Id,
            Amount = amount,
            Reason = $"Return {request.RmaNumber}",
            Status = RefundStatus.Completed,
            StaffUserId = staffUserId,
            ProcessedAtUtc = DateTime.UtcNow
        };
        db.Refunds.Add(refund);

        var totalRefunded = previouslyRefunded + amount;
        order.PaymentStatus = totalRefunded >= order.Total
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        request.Status = ReturnStatus.Refunded;
        request.ProcessedByUserId = staffUserId;
        request.ResolvedAtUtc = DateTime.UtcNow;

        var amountText = amount.ToString("0.000", CultureInfo.InvariantCulture);
        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Kind = "return.refunded",
            Description = $"Return {request.RmaNumber} refunded KWD {amountText} (manual). Order payment status: {order.PaymentStatus}.",
            AuthorUserId = staffUserId
        });

        await db.SaveChangesAsync(ct);

        // Confirm the refund to the customer (same template as a standalone order refund). Guarded.
        await emailService.SendOrderRefundedAsync(order, refund, ct);

        return new ReturnActionResult(true,
            $"Return {request.RmaNumber} refunded — KWD {amountText}.",
            request.OrderId, request.RmaNumber,
            nameof(ReturnStatus.Received), nameof(ReturnStatus.Refunded));
    }
}
