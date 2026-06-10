using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Core.Models.Payments;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services.Admin;

public class OrderAdminService(WhiteStichesDbContext db, IOrderService orders, IPaymentGateway gateway) : IOrderAdminService
{
    public async Task<PagedResult<Order>> GetOrdersAdminAsync(OrderStatus? status, PaymentStatus? paymentStatus,
        OrderChannel? channel, string? search, bool isDraft = false,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.IsDraft == isDraft);

        if (status is not null) query = query.Where(o => o.Status == status);
        if (paymentStatus is not null) query = query.Where(o => o.PaymentStatus == paymentStatus);
        if (channel is not null) query = query.Where(o => o.Channel == channel);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                o.Email.Contains(term) ||
                o.Phone.Contains(term) ||
                (o.ShipFirstName + " " + o.ShipLastName).Contains(term));
        }

        query = query.OrderByDescending(o => o.PlacedAtUtc ?? o.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<Order> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<Order?> GetDetailAsync(int id, CancellationToken ct = default) =>
        db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Events.OrderByDescending(e => e.CreatedAtUtc))
            .Include(o => o.Payments.OrderByDescending(p => p.CreatedAtUtc))
            .Include(o => o.Refunds.OrderByDescending(r => r.CreatedAtUtc))
            .Include(o => o.Shipments.OrderByDescending(s => s.CreatedAtUtc))
            .Include(o => o.ReturnRequests.OrderByDescending(r => r.CreatedAtUtc))
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Payment> MarkPaidAsync(int orderId, decimal? amount, string? reference,
        Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var amt = amount is > 0 ? amount.Value : order.Total;

        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = "Manual",
            Method = "manual",
            Status = TransactionStatus.Captured,
            GatewayTransactionId = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            Amount = amt,
            Currency = order.Currency,
            ProcessedAtUtc = DateTime.UtcNow
        };
        db.Payments.Add(payment);

        order.PaymentStatus = PaymentStatus.Paid;
        if (order.Status == OrderStatus.Placed)
        {
            order.Status = OrderStatus.Paid;
        }

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Kind = "payment.manual",
            Description = $"Manual payment of {amt.ToString("0.000")} KWD recorded"
                + (string.IsNullOrWhiteSpace(reference) ? "." : $" (ref: {reference.Trim()})."),
            AuthorUserId = staffUserId,
            AuthorName = staffName
        });

        await db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<Shipment> FulfillAsync(int orderId, string? carrier, string? trackingNumber, string? trackingUrl,
        Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var shipment = new Shipment
        {
            OrderId = order.Id,
            Carrier = string.IsNullOrWhiteSpace(carrier) ? null : carrier.Trim(),
            AwbNumber = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber.Trim(),
            TrackingUrl = string.IsNullOrWhiteSpace(trackingUrl) ? null : trackingUrl.Trim(),
            Status = ShipmentStatus.LabelCreated
        };
        db.Shipments.Add(shipment);

        foreach (var item in order.Items)
        {
            item.FulfilledQuantity = item.Quantity;
        }

        order.FulfillmentStatus = FulfillmentStatus.Fulfilled;
        if (order.Status is OrderStatus.Paid or OrderStatus.Fulfilled)
        {
            order.Status = OrderStatus.Shipped;
        }

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Kind = "shipment",
            Description = "Shipment created"
                + (shipment.Carrier is null ? "" : $" via {shipment.Carrier}")
                + (shipment.AwbNumber is null ? "." : $" (AWB {shipment.AwbNumber})."),
            AuthorUserId = staffUserId,
            AuthorName = staffName
        });

        await db.SaveChangesAsync(ct);
        return shipment;
    }

    public async Task<Refund> RefundAsync(int orderId, decimal amount, string? reason,
        Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Payments)
            .Include(o => o.Refunds)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var totalPaid = order.Payments.Where(p => p.Status == TransactionStatus.Captured).Sum(p => p.Amount);
        var totalRefunded = order.Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);
        var remaining = totalPaid - totalRefunded;

        if (amount <= 0)
        {
            throw new InvalidOperationException("Refund amount must be greater than zero.");
        }

        if (amount > remaining)
        {
            throw new InvalidOperationException(
                $"Refund amount exceeds the remaining refundable {remaining.ToString("0.000")} KWD.");
        }

        var lastPayment = order.Payments
            .Where(p => p.Status == TransactionStatus.Captured)
            .OrderByDescending(p => p.ProcessedAtUtc ?? p.CreatedAtUtc)
            .FirstOrDefault();

        // Issue the refund at the gateway for Tap-captured payments before recording it.
        // Manual ("Manual" provider) payments are recorded locally only, as before.
        string? gatewayRefundId = null;
        if (lastPayment is { Provider: "Tap", GatewayTransactionId: { Length: > 0 } chargeId })
        {
            if (!gateway.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Tap is not configured on the Admin app — set Tap:SecretKey before refunding gateway payments.");
            }

            var refundResult = await gateway.CreateRefundAsync(new PaymentRefundRequest
            {
                ChargeId = chargeId,
                Amount = amount,
                Currency = order.Currency,
                Reason = string.IsNullOrWhiteSpace(reason) ? "requested_by_customer" : reason.Trim(),
                Description = $"Refund for order {order.OrderNumber}"
            }, ct);

            if (!refundResult.Success)
            {
                throw new InvalidOperationException($"Tap refund failed: {refundResult.Error ?? "unknown error"}.");
            }

            gatewayRefundId = refundResult.RefundId;
        }

        var refund = new Refund
        {
            OrderId = order.Id,
            PaymentId = lastPayment?.Id,
            Amount = amount,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = RefundStatus.Completed,
            GatewayRefundId = gatewayRefundId,
            StaffUserId = staffUserId,
            ProcessedAtUtc = DateTime.UtcNow
        };
        db.Refunds.Add(refund);

        order.PaymentStatus = totalRefunded + amount >= totalPaid
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Kind = "refund",
            Description = $"Refund of {amount.ToString("0.000")} KWD recorded"
                + (refund.Reason is null ? "." : $". Reason: {refund.Reason}"),
            AuthorUserId = staffUserId,
            AuthorName = staffName
        });

        await db.SaveChangesAsync(ct);
        return refund;
    }

    public async Task SaveInternalNoteAsync(int orderId, string? internalNote, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        order.InternalNote = string.IsNullOrWhiteSpace(internalNote) ? null : internalNote.Trim();
        await db.SaveChangesAsync(ct);
    }

    // ---- Draft orders (AD-ORD-08) ----

    public async Task<Order> CreateDraftAsync(string email, string phone, string firstName, string lastName,
        OrderChannel channel, Guid? staffUserId = null, string? staffName = null, CancellationToken ct = default)
    {
        var draft = new Order
        {
            OrderNumber = await orders.GenerateOrderNumberAsync(ct),
            Email = email.Trim(),
            Phone = phone.Trim(),
            ShipFirstName = firstName.Trim(),
            ShipLastName = lastName.Trim(),
            Channel = channel,
            IsDraft = true,
            Status = OrderStatus.Placed,
            PaymentStatus = PaymentStatus.Pending,
            FulfillmentStatus = FulfillmentStatus.Unfulfilled,
            Currency = "KWD"
        };

        draft.Events.Add(new OrderEvent
        {
            Kind = "draft",
            Description = $"Draft order {draft.OrderNumber} created for a {channel} sale.",
            AuthorUserId = staffUserId,
            AuthorName = staffName
        });

        db.Orders.Add(draft);
        await db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task AddDraftItemAsync(int orderId, int variantId, int quantity, CancellationToken ct = default)
    {
        var order = await GetTrackedDraftAsync(orderId, ct);

        if (quantity < 1) quantity = 1;

        var variant = await db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p.Images)
            .Include(v => v.Image)
            .FirstOrDefaultAsync(v => v.Id == variantId, ct)
            ?? throw new InvalidOperationException($"Variant {variantId} not found.");

        var existing = order.Items.FirstOrDefault(i => i.ProductVariantId == variant.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            existing.LineTotal = existing.UnitPrice * existing.Quantity;
        }
        else
        {
            var options = new[] { variant.Option1, variant.Option2, variant.Option3 }
                .Where(o => !string.IsNullOrWhiteSpace(o));

            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = variant.ProductId,
                ProductVariantId = variant.Id,
                TitleEn = variant.Product.TitleEn,
                TitleAr = variant.Product.TitleAr,
                VariantDescription = string.Join(" / ", options) is { Length: > 0 } desc ? desc : null,
                Sku = variant.Sku,
                ImageUrl = variant.Image?.Url
                    ?? variant.Product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
                UnitPrice = variant.Price,
                Quantity = quantity,
                LineTotal = variant.Price * quantity
            });
        }

        RecomputeTotals(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveDraftItemAsync(int orderId, int itemId, CancellationToken ct = default)
    {
        var order = await GetTrackedDraftAsync(orderId, ct);

        var item = order.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Line item {itemId} not found on this draft.");

        order.Items.Remove(item);
        db.OrderItems.Remove(item);

        RecomputeTotals(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateDraftAsync(int orderId, DraftOrderUpdate update, CancellationToken ct = default)
    {
        var order = await GetTrackedDraftAsync(orderId, ct);

        order.ShippingAmount = Math.Max(0, update.ShippingAmount);
        order.DiscountAmount = Math.Max(0, update.DiscountAmount);
        order.CustomerNote = string.IsNullOrWhiteSpace(update.CustomerNote) ? null : update.CustomerNote.Trim();

        if (update.Email is not null) order.Email = update.Email.Trim();
        if (update.Phone is not null) order.Phone = update.Phone.Trim();
        if (update.ShipFirstName is not null) order.ShipFirstName = update.ShipFirstName.Trim();
        if (update.ShipLastName is not null) order.ShipLastName = update.ShipLastName.Trim();
        if (update.ShipPhone is not null) order.ShipPhone = update.ShipPhone.Trim();
        if (update.ShipGovernorate is not null) order.ShipGovernorate = update.ShipGovernorate.Trim();
        if (update.ShipArea is not null) order.ShipArea = update.ShipArea.Trim();
        if (update.ShipBlock is not null) order.ShipBlock = update.ShipBlock.Trim();
        if (update.ShipStreet is not null) order.ShipStreet = update.ShipStreet.Trim();
        if (update.ShipBuilding is not null) order.ShipBuilding = update.ShipBuilding.Trim();
        order.ShipFloor = string.IsNullOrWhiteSpace(update.ShipFloor) ? null : update.ShipFloor.Trim();
        order.ShipApartment = string.IsNullOrWhiteSpace(update.ShipApartment) ? null : update.ShipApartment.Trim();
        order.ShipDirections = string.IsNullOrWhiteSpace(update.ShipDirections) ? null : update.ShipDirections.Trim();

        RecomputeTotals(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task ConvertDraftAsync(int orderId, Guid? staffUserId = null, string? staffName = null,
        CancellationToken ct = default)
    {
        var order = await GetTrackedDraftAsync(orderId, ct);

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one item before converting this draft.");
        }

        foreach (var item in order.Items.Where(i => i.ProductVariantId is not null))
        {
            var variant = await db.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId!.Value, ct);
            if (variant is null) continue;

            if (!variant.AllowOversell && variant.StockQuantity < item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for {item.TitleEn}" +
                    (item.VariantDescription is null ? "" : $" ({item.VariantDescription})") +
                    $": {variant.StockQuantity} available, {item.Quantity} requested.");
            }

            variant.StockQuantity -= item.Quantity;
            db.InventoryAdjustments.Add(new InventoryAdjustment
            {
                ProductVariantId = variant.Id,
                QuantityDelta = -item.Quantity,
                Reason = InventoryAdjustmentReason.Sale,
                Note = $"Draft order {order.OrderNumber} converted",
                StaffUserId = staffUserId
            });
        }

        order.IsDraft = false;
        order.Status = OrderStatus.Placed;
        order.PlacedAtUtc = DateTime.UtcNow;
        RecomputeTotals(order);

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Kind = "draft.converted",
            Description = $"Draft converted to order {order.OrderNumber}.",
            AuthorUserId = staffUserId,
            AuthorName = staffName
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDraftAsync(int orderId, CancellationToken ct = default)
    {
        var order = await GetTrackedDraftAsync(orderId, ct);

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Product>> SearchProductsForDraftAsync(string search, int take = 20,
        CancellationToken ct = default)
    {
        var query = db.Products
            .AsNoTracking()
            .Include(p => p.Variants.Where(v => v.IsActive).OrderBy(v => v.Position))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Where(p => p.Status == ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.TitleEn.Contains(term) ||
                p.TitleAr.Contains(term) ||
                p.Slug.Contains(term) ||
                p.Variants.Any(v => v.Sku != null && v.Sku.Contains(term)));
        }

        return await query
            .OrderBy(p => p.TitleEn)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    // ---- helpers ----

    private async Task<Order> GetTrackedDraftAsync(int orderId, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        return order.IsDraft
            ? order
            : throw new InvalidOperationException($"Order {order.OrderNumber} is not a draft.");
    }

    private static void RecomputeTotals(Order order)
    {
        order.Subtotal = order.Items.Sum(i => i.LineTotal);
        order.Total = Math.Max(0, order.Subtotal - order.DiscountAmount)
            + order.ShippingAmount + order.GiftWrapFee + order.TaxAmount;
    }
}
