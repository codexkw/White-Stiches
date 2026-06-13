using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class OrderService(WhiteStichesDbContext db, IEmailService emailService) : IOrderService
{
    private const int OrderNumberSeedBase = 10000;

    public async Task<Order> CreateOrderAsync(Order order, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(order.OrderNumber))
        {
            order.OrderNumber = await GenerateOrderNumberAsync(ct);
        }

        order.PlacedAtUtc ??= DateTime.UtcNow;
        order.Events.Add(new OrderEvent
        {
            Kind = "placed",
            Description = $"Order {order.OrderNumber} placed via {order.Channel}."
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Orders
            .Include(o => o.Items)
            .Include(o => o.Events.OrderByDescending(e => e.CreatedAtUtc))
            .Include(o => o.Payments)
            .Include(o => o.Refunds)
            .Include(o => o.Shipments)
            .Include(o => o.ReturnRequests)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct = default) =>
        db.Orders
            .Include(o => o.Items)
            .Include(o => o.Events.OrderByDescending(e => e.CreatedAtUtc))
            .Include(o => o.Payments)
            .Include(o => o.Refunds)
            .Include(o => o.Shipments)
            .Include(o => o.ReturnRequests)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public Task<Order?> TrackAsync(string orderNumber, string emailOrPhone, CancellationToken ct = default)
    {
        var contact = emailOrPhone.Trim();
        return db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber
                && (o.Email == contact || o.Phone == contact), ct);
    }

    public async Task<PagedResult<Order>> GetOrdersForCustomerAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var query = db.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId && !o.IsDraft)
            .Include(o => o.Items)
            .OrderByDescending(o => o.PlacedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<Order> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<Order>> GetOrdersAsync(OrderStatus? status, PaymentStatus? paymentStatus, string? search,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();

        if (status is not null) query = query.Where(o => o.Status == status);
        if (paymentStatus is not null) query = query.Where(o => o.PaymentStatus == paymentStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                o.Email.Contains(term) ||
                o.Phone.Contains(term) ||
                (o.ShipFirstName + " " + o.ShipLastName).Contains(term));
        }

        query = query.OrderByDescending(o => o.PlacedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<Order> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task UpdateStatusAsync(int orderId, OrderStatus status, Guid? staffUserId = null, CancellationToken ct = default)
    {
        var order = await db.Orders.FindAsync([orderId], ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var previous = order.Status;
        order.Status = status;

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = orderId,
            Kind = "system",
            Description = $"Status changed from {previous} to {status}.",
            AuthorUserId = staffUserId
        });

        await db.SaveChangesAsync(ct);

        // A delivery confirmation is the one status change customers care about by email. Guarded.
        if (status == OrderStatus.Delivered)
            await emailService.SendOrderDeliveredAsync(order, ct);
    }

    public async Task AddEventAsync(int orderId, string kind, string description,
        Guid? authorUserId = null, string? authorName = null, CancellationToken ct = default)
    {
        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = orderId,
            Kind = kind,
            Description = description,
            AuthorUserId = authorUserId,
            AuthorName = authorName
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int orderId, string reason, bool restock, Guid? staffUserId = null, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;
        order.CancelReason = reason;

        if (restock)
        {
            foreach (var item in order.Items.Where(i => i.ProductVariantId is not null))
            {
                var variant = await db.ProductVariants.FindAsync([item.ProductVariantId!.Value], ct);
                if (variant is not null)
                {
                    variant.StockQuantity += item.Quantity;
                    db.InventoryAdjustments.Add(new Core.Entities.Catalog.InventoryAdjustment
                    {
                        ProductVariantId = variant.Id,
                        QuantityDelta = item.Quantity,
                        Reason = InventoryAdjustmentReason.ReturnRestock,
                        Note = $"Order {order.OrderNumber} cancelled",
                        StaffUserId = staffUserId
                    });
                }
            }
        }

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = orderId,
            Kind = "system",
            Description = $"Order cancelled. Reason: {reason}",
            AuthorUserId = staffUserId
        });

        await db.SaveChangesAsync(ct);

        // Notify the customer their order was cancelled (+ a refund note if it was paid). Guarded.
        await emailService.SendOrderCancelledAsync(order, ct);
    }

    public async Task<ReturnRequest> CreateReturnRequestAsync(ReturnRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.RmaNumber))
        {
            var count = await db.ReturnRequests.CountAsync(ct);
            request.RmaNumber = $"RMA-{1000 + count + 1}";
        }

        db.ReturnRequests.Add(request);

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = request.OrderId,
            Kind = "return",
            Description = $"Return request {request.RmaNumber} submitted."
        });

        await db.SaveChangesAsync(ct);

        // Acknowledge the RMA to the customer and alert staff that a return needs review. Guarded.
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is not null)
        {
            await emailService.SendReturnRequestedAsync(order, request, ct);
            await emailService.SendNewReturnNotificationAsync(order, request, ct);
        }

        return request;
    }

    public async Task<PagedResult<ReturnRequest>> GetReturnRequestsAsync(ReturnStatus? status,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = db.ReturnRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .Include(r => r.Items)
                .ThenInclude(i => i.OrderItem)
            .AsQueryable();

        if (status is not null) query = query.Where(r => r.Status == status);

        query = query.OrderByDescending(r => r.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<ReturnRequest> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task UpdateReturnStatusAsync(int returnRequestId, ReturnStatus status,
        string? staffNote = null, Guid? staffUserId = null, CancellationToken ct = default)
    {
        var request = await db.ReturnRequests.FindAsync([returnRequestId], ct)
            ?? throw new InvalidOperationException($"Return request {returnRequestId} not found.");

        request.Status = status;
        if (staffNote is not null) request.StaffNote = staffNote;
        request.ProcessedByUserId = staffUserId;

        if (status is ReturnStatus.Refunded or ReturnStatus.Rejected)
        {
            request.ResolvedAtUtc = DateTime.UtcNow;
        }

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = request.OrderId,
            Kind = "return",
            Description = $"Return {request.RmaNumber} marked {status}.",
            AuthorUserId = staffUserId
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<string> GenerateOrderNumberAsync(CancellationToken ct = default)
    {
        // Sequential, human-friendly numbers: WS-10001, WS-10002, ...
        var lastId = await db.Orders.AnyAsync(ct)
            ? await db.Orders.MaxAsync(o => o.Id, ct)
            : 0;

        return $"WS-{OrderNumberSeedBase + lastId + 1}";
    }
}
