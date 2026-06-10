using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Back-office order operations beyond IOrderService: fulfilment/shipments, manual
/// payments, refunds, internal notes, draft orders (AD-ORD-01..05, AD-ORD-08).
/// Owned by the Orders admin module.
/// </summary>
public interface IOrderAdminService
{
    /// <summary>Richer order list than IOrderService.GetOrdersAsync — adds channel + draft filters.</summary>
    Task<PagedResult<Order>> GetOrdersAdminAsync(OrderStatus? status, PaymentStatus? paymentStatus,
        OrderChannel? channel, string? search, bool isDraft = false,
        int page = 1, int pageSize = 25, CancellationToken ct = default);

    /// <summary>Full detail: items, events, payments, refunds, shipments, return requests.</summary>
    Task<Order?> GetDetailAsync(int id, CancellationToken ct = default);

    /// <summary>Records a manual payment (Provider "Manual") and marks the order paid. Null/zero amount defaults to the order total.</summary>
    Task<Payment> MarkPaidAsync(int orderId, decimal? amount, string? reference,
        Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default);

    /// <summary>Creates a shipment (label created) and marks the order fulfilled (shipped when it was paid/fulfilled).</summary>
    Task<Shipment> FulfillAsync(int orderId, string? carrier, string? trackingNumber, string? trackingUrl,
        Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default);

    /// <summary>Records a manual refund. Throws when amount is not positive or exceeds the remaining refundable amount.</summary>
    Task<Refund> RefundAsync(int orderId, decimal amount, string? reason,
        Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default);

    Task SaveInternalNoteAsync(int orderId, string? internalNote, CancellationToken ct = default);

    // ---- Draft orders (AD-ORD-08) ----

    /// <summary>Creates an empty draft order for a phone/WhatsApp/Instagram sale.</summary>
    Task<Order> CreateDraftAsync(string email, string phone, string firstName, string lastName,
        OrderChannel channel, Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default);

    /// <summary>Adds (or merges) a line snapshotting title/price from the catalog, then recomputes totals.</summary>
    Task AddDraftItemAsync(int orderId, int variantId, int quantity, CancellationToken ct = default);

    Task RemoveDraftItemAsync(int orderId, int itemId, CancellationToken ct = default);

    Task UpdateDraftAsync(int orderId, DraftOrderUpdate update, CancellationToken ct = default);

    /// <summary>Converts a draft to a live order: validates items, decrements stock (respects AllowOversell), sets Placed.</summary>
    Task ConvertDraftAsync(int orderId, Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default);

    Task DeleteDraftAsync(int orderId, CancellationToken ct = default);

    /// <summary>No-JS product picker: active products (with active variants) matching a search term.</summary>
    Task<IReadOnlyList<Product>> SearchProductsForDraftAsync(string search, int take = 20, CancellationToken ct = default);
}
