using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Returns queue flow: approve / reject / receive / refund with restock and refund
/// records (AD-ORD-10). Owned by the Returns admin module.
/// </summary>
public interface IReturnAdminService
{
    /// <summary>Paged queue with order + items included; null status = all.</summary>
    Task<PagedResult<ReturnRequest>> GetQueueAsync(ReturnStatus? status, int page = 1, int pageSize = 25, CancellationToken ct = default);

    /// <summary>Full graph: order (with refunds + events), items with order-item snapshots.</summary>
    Task<ReturnRequest?> GetDetailAsync(int id, CancellationToken ct = default);

    /// <summary>Pending → Approved. Staff note optional.</summary>
    Task<ReturnActionResult> ApproveAsync(int id, string? staffNote, Guid? staffUserId, CancellationToken ct = default);

    /// <summary>Pending → Rejected. Staff note required.</summary>
    Task<ReturnActionResult> RejectAsync(int id, string staffNote, Guid? staffUserId, CancellationToken ct = default);

    /// <summary>
    /// Approved → Received. When <paramref name="restock"/> is true, each returned
    /// quantity is added back to its variant's stock (skipped silently when the
    /// variant no longer exists; the skip is noted on the order event).
    /// </summary>
    Task<ReturnActionResult> ReceiveAsync(int id, bool restock, Guid? staffUserId, CancellationToken ct = default);

    /// <summary>
    /// Received → Refunded. Records a manual completed refund on the order, updates the
    /// order payment status to Refunded/PartiallyRefunded, and logs a "return.refunded" event.
    /// </summary>
    Task<ReturnActionResult> RefundAsync(int id, decimal amount, Guid? staffUserId, CancellationToken ct = default);
}
