using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Order lifecycle — creation, timeline, fulfilment, returns (AD-ORD-*).</summary>
public interface IOrderService
{
    Task<Order> CreateOrderAsync(Order order, CancellationToken ct = default);

    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct = default);

    /// <summary>Public order tracking lookup: order number + email or phone (SF-STA-06).</summary>
    Task<Order?> TrackAsync(string orderNumber, string emailOrPhone, CancellationToken ct = default);

    Task<PagedResult<Order>> GetOrdersForCustomerAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<PagedResult<Order>> GetOrdersAsync(OrderStatus? status, PaymentStatus? paymentStatus, string? search, int page = 1, int pageSize = 25, CancellationToken ct = default);

    Task UpdateStatusAsync(int orderId, OrderStatus status, Guid? staffUserId = null, CancellationToken ct = default);
    Task AddEventAsync(int orderId, string kind, string description, Guid? authorUserId = null, string? authorName = null, CancellationToken ct = default);
    Task CancelAsync(int orderId, string reason, bool restock, Guid? staffUserId = null, CancellationToken ct = default);

    Task<ReturnRequest> CreateReturnRequestAsync(ReturnRequest request, CancellationToken ct = default);
    Task<PagedResult<ReturnRequest>> GetReturnRequestsAsync(ReturnStatus? status, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task UpdateReturnStatusAsync(int returnRequestId, ReturnStatus status, string? staffNote = null, Guid? staffUserId = null, CancellationToken ct = default);

    /// <summary>Generates the next sequential human-facing order number (e.g., "WS-10001").</summary>
    Task<string> GenerateOrderNumberAsync(CancellationToken ct = default);
}
