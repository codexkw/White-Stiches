using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models.Payments;
using WhiteStiches.Web.Infrastructure;

namespace WhiteStiches.Web.Controllers;

/// <summary>
/// Tap Payments redirect return + server-to-server webhook (Phase 1C). Both converge on
/// the same idempotent finalizer, so payment is confirmed even if one path is missed
/// (e.g. the customer closes the browser, or the webhook can't reach a local dev host).
/// </summary>
public class PaymentsController(
    IPaymentGateway gateway,
    IPaymentService paymentService,
    ICurrentCartAccessor cartAccessor,
    ICartService cartService,
    ILogger<PaymentsController> logger) : Controller
{
    /// <summary>Browser return from the Tap hosted page (Tap appends ?tap_id=chg_xxx).</summary>
    [HttpGet("checkout/tap-return")]
    public async Task<IActionResult> TapReturn([FromQuery(Name = "tap_id")] string? tapId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tapId))
            return Redirect("/checkout");

        // Authoritative status — never trust the redirect alone.
        var status = await gateway.RetrieveChargeAsync(tapId, ct);

        if (status.IsCaptured)
        {
            var result = await paymentService.FinalizeCapturedChargeAsync(tapId, status.Amount, status.RawJson, ct);

            switch (result.Outcome)
            {
                case OrderFinalizeOutcome.Finalized:
                case OrderFinalizeOutcome.AlreadyFinalized:
                    // Back in the browser session: clear the (still-intact) cart and the resume
                    // cookie. Best-effort — the webhook may already have finalized.
                    try
                    {
                        var cart = await cartAccessor.GetCartAsync(ct);
                        await cartService.ClearAsync(cart.Id, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Clearing the cart after payment failed (non-fatal).");
                    }

                    Response.Cookies.Delete("ws_pay");
                    TempData["LastOrderNumber"] = result.OrderNumber!;
                    return Redirect($"/checkout/confirmation/{result.OrderNumber}");

                case OrderFinalizeOutcome.AmountMismatch:
                    logger.LogError("Tap charge {ChargeId} captured with an amount mismatch — held for review.", tapId);
                    TempData["CheckoutError"] =
                        $"We received your payment but the amount didn’t match your order. Please contact support and quote {tapId}.";
                    return Redirect("/checkout");

                default: // NotFound — captured at Tap but no order row to reconcile against
                    logger.LogError("Tap charge {ChargeId} is CAPTURED but no matching order was found.", tapId);
                    TempData["CheckoutError"] =
                        $"We received your payment but couldn’t locate your order. Please contact support and quote {tapId}.";
                    return Redirect("/checkout");
            }
        }

        // Not captured (abandoned/declined/failed): let the customer retry with their bag intact.
        await paymentService.MarkChargeFailedAsync(tapId, status.RawStatus, status.RawJson, ct);
        TempData["CheckoutError"] = "Your payment was not completed. You can try again below.";
        return Redirect("/checkout");
    }

    /// <summary>Tap server-to-server webhook. Authenticity is proven by the "hashstring" header.</summary>
    [HttpPost("payments/tap/webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TapWebhook(CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["hashstring"].FirstOrDefault();
        var result = gateway.ParseAndVerifyWebhook(body, signature);

        if (!result.IsValid)
        {
            logger.LogWarning("Rejected Tap webhook (invalid signature) for charge {ChargeId}.", result.ChargeId);
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(result.ChargeId))
            return Ok();

        if (result.IsCaptured)
            await paymentService.FinalizeCapturedChargeAsync(result.ChargeId, result.Amount, body, ct);
        else if (result.State == GatewayChargeState.Failed)
            await paymentService.MarkChargeFailedAsync(result.ChargeId, result.RawStatus, body, ct);

        // Always 200 on a verified, well-formed webhook so Tap stops retrying.
        return Ok();
    }
}
