using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models.Payments;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

/// <summary>
/// Order-aware payment orchestration over <see cref="IPaymentGateway"/>. The finalizer
/// is safe to call from both the browser return and the server webhook: an atomic,
/// conditional claim (UPDATE … WHERE Status &lt;&gt; Captured) inside a transaction lets
/// exactly one caller capture the payment and decrement stock; the loser is a no-op.
/// </summary>
public class PaymentService(
    WhiteStichesDbContext db,
    IPaymentGateway gateway,
    IEmailService emailService,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentChargeResult> StartChargeForOrderAsync(int orderId, string redirectUrl, string? webhookUrl,
        CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var payment = order.Payments.Where(p => p.Provider == "Tap").OrderByDescending(p => p.Id).FirstOrDefault()
            ?? order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();

        // Refuse to open a charge we couldn't record: a charge created at Tap with no Payment row to
        // hold its id can never be reconciled by the return or webhook (GatewayTransactionId stays null),
        // leaving the customer charged with no way to finalize the order. Fail loud instead.
        if (payment is null)
            throw new InvalidOperationException(
                $"Order {order.OrderNumber} has no Payment row to record the gateway charge against.");

        var result = await gateway.CreateChargeAsync(new PaymentChargeRequest
        {
            Amount = order.Total,
            Currency = order.Currency,
            OrderNumber = order.OrderNumber,
            Description = $"White Stitches order {order.OrderNumber}",
            CustomerFirstName = order.ShipFirstName,
            CustomerLastName = order.ShipLastName,
            CustomerEmail = order.Email,
            CustomerPhoneNumber = order.Phone,
            RedirectUrl = redirectUrl,
            WebhookUrl = webhookUrl
        }, ct);

        if (payment is not null)
        {
            if (result.Success)
            {
                payment.GatewayTransactionId = result.ChargeId;
                payment.ResponseJson = result.RawJson;
            }
            else
            {
                payment.Status = TransactionStatus.Failed;
                payment.ResponseJson = result.RawJson;
                db.OrderEvents.Add(new OrderEvent
                {
                    OrderId = order.Id,
                    Kind = "payment.failed",
                    Description = $"Could not start the Tap payment: {result.Error ?? "unknown error"}."
                });
            }

            await db.SaveChangesAsync(ct);
        }

        return result;
    }

    public async Task<OrderFinalizeResult> FinalizeCapturedChargeAsync(string chargeId, decimal? capturedAmount = null,
        string? responseJson = null, CancellationToken ct = default)
    {
        var payment = await db.Payments
            .Include(p => p.Order).ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(p => p.GatewayTransactionId == chargeId, ct);

        if (payment is null)
        {
            logger.LogWarning("Finalize: no payment matches Tap charge {ChargeId}.", chargeId);
            return OrderFinalizeResult.NotFound;
        }

        var order = payment.Order;

        // Reconcile the gateway-captured amount against the order total before releasing goods.
        // The charge is created server-side from order.Total, so a mismatch should never happen —
        // if it does, refuse to mark paid and flag the order for manual review.
        if (capturedAmount is { } amount)
        {
            var decimals = CurrencyDecimals(order.Currency);
            if (Math.Round(amount, decimals) != Math.Round(order.Total, decimals))
            {
                logger.LogError(
                    "Tap charge {ChargeId} captured {Captured} but order {OrderNumber} total is {Total} — not marking paid.",
                    chargeId, amount, order.OrderNumber, order.Total);

                db.OrderEvents.Add(new OrderEvent
                {
                    OrderId = order.Id,
                    Kind = "payment.amount_mismatch",
                    Description = $"Tap charge {chargeId} captured {amount.ToString("0.000")} {order.Currency} "
                                + $"but order total is {order.Total.ToString("0.000")} {order.Currency}. Needs review."
                });
                await db.SaveChangesAsync(ct);
                return new OrderFinalizeResult(OrderFinalizeOutcome.AmountMismatch, order.OrderNumber);
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Atomic claim — only one trigger (return vs webhook) flips Initiated → Captured.
        var claimed = await db.Payments
            .Where(p => p.Id == payment.Id && p.Status != TransactionStatus.Captured)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, TransactionStatus.Captured)
                .SetProperty(p => p.ProcessedAtUtc, DateTime.UtcNow)
                .SetProperty(p => p.ResponseJson, responseJson ?? payment.ResponseJson), ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return new OrderFinalizeResult(OrderFinalizeOutcome.AlreadyFinalized, order.OrderNumber);
        }

        order.PaymentStatus = PaymentStatus.Paid;
        if (order.Status == OrderStatus.Placed)
            order.Status = OrderStatus.Paid;

        // Decrement stock with an atomic DB update (StockQuantity = StockQuantity - qty) so two
        // different orders for the same variant can never lose each other's decrement.
        foreach (var item in order.Items.Where(i => i.ProductVariantId is not null))
        {
            var variantId = item.ProductVariantId!.Value;
            var qty = item.Quantity;

            var affected = await db.ProductVariants
                .Where(v => v.Id == variantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - qty), ct);

            if (affected == 0) continue; // variant deleted since the order was placed

            db.InventoryAdjustments.Add(new InventoryAdjustment
            {
                ProductVariantId = variantId,
                QuantityDelta = -qty,
                Reason = InventoryAdjustmentReason.Sale,
                Note = $"Order {order.OrderNumber} paid (Tap {chargeId})"
            });
        }

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Kind = "payment.captured",
            Description = $"Payment captured via Tap ({chargeId}). {order.Total.ToString("0.000")} {order.Currency}."
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Confirmation email — fired AFTER commit (outside the transaction) so a slow/failed SMTP
        // never holds DB locks or rolls back a captured payment. The service swallows its own errors.
        await emailService.SendOrderConfirmationAsync(order, ct);

        logger.LogInformation("Order {OrderNumber} finalized from Tap charge {ChargeId}.", order.OrderNumber, chargeId);
        return new OrderFinalizeResult(OrderFinalizeOutcome.Finalized, order.OrderNumber);
    }

    public async Task MarkChargeFailedAsync(string chargeId, string? rawStatus, string? responseJson = null,
        CancellationToken ct = default)
    {
        var payment = await db.Payments
            .FirstOrDefaultAsync(p => p.GatewayTransactionId == chargeId, ct);

        if (payment is null) return;
        if (payment.Status is TransactionStatus.Captured or TransactionStatus.Failed) return;

        // Atomic, conditional transition so a capture that commits concurrently (the webhook
        // firing CAPTURED while the browser-return path sees not-captured) is never overwritten
        // with Failed. Only the caller that actually flips the row writes the failure event.
        var claimed = await db.Payments
            .Where(p => p.Id == payment.Id
                     && p.Status != TransactionStatus.Captured
                     && p.Status != TransactionStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, TransactionStatus.Failed)
                .SetProperty(p => p.ProcessedAtUtc, DateTime.UtcNow)
                .SetProperty(p => p.ResponseJson, responseJson ?? payment.ResponseJson), ct);

        if (claimed == 0) return; // a capture (or a prior failure) already won the race

        db.OrderEvents.Add(new OrderEvent
        {
            OrderId = payment.OrderId,
            Kind = "payment.failed",
            Description = $"Tap charge {chargeId} did not complete ({rawStatus ?? "unknown"})."
        });

        await db.SaveChangesAsync(ct);
    }

    private static int CurrencyDecimals(string currency) => currency.ToUpperInvariant() switch
    {
        "KWD" or "BHD" or "OMR" or "JOD" or "TND" or "LYD" => 3,
        "JPY" or "KRW" => 0,
        _ => 2
    };
}
