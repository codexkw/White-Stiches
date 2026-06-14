using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Entities.ShoppingCart;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Web.Infrastructure;
using WhiteStiches.Web.Models.Checkout;

namespace WhiteStiches.Web.Controllers;

public class CheckoutController(
    ICurrentCartAccessor cartAccessor,
    ICartService cartService,
    ISettingsService settingsService,
    ICatalogService catalogService,
    IOrderService orderService,
    IPaymentGateway paymentGateway,
    IPaymentService paymentService,
    ICustomerService customerService,
    IEmailService emailService,
    IConfiguration configuration,
    IWebHostEnvironment env,
    UserManager<ApplicationUser> userManager) : Controller
{
    /// <summary>Points the browser at its in-flight Tap order so a re-submit resumes it instead of duplicating.</summary>
    private const string PendingPayCookie = "ws_pay";

    [HttpGet("checkout")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cart = await cartAccessor.GetCartAsync(ct);
        if (cart.Items.Count == 0) return Redirect("/cart");

        var form = new CheckoutFormModel();
        await PrefillAsync(form, ct);

        var vm = await BuildViewModelAsync(cart, form, ct);
        return View(vm);
    }

    [HttpPost("checkout/place")]
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(CheckoutFormModel form, CancellationToken ct)
    {
        var cart = await cartAccessor.GetCartAsync(ct);
        if (cart.Items.Count == 0) return Redirect("/cart");

        if (!form.TermsAccepted)
        {
            ModelState.AddModelError(nameof(CheckoutFormModel.TermsAccepted),
                "Please accept the Terms of Sale, Privacy Policy, and Returns Policy to continue.");
        }

        if (!ModelState.IsValid)
        {
            var vm = await BuildViewModelAsync(cart, form, ct);
            return View("Index", vm);
        }

        // Re-verify stock for every line before committing the order
        foreach (var item in cart.Items)
        {
            var variant = item.ProductVariant;
            if (!variant.AllowOversell && variant.StockQuantity < item.Quantity)
            {
                TempData["CartError"] =
                    $"“{variant.Product.TitleEn}” no longer has enough stock. Please review your bag.";
                return Redirect("/cart");
            }
        }

        var summary = await cartService.GetSummaryAsync(cart.Id, ct);

        var (standardRate, expressRate, sameDayRate) = await LoadShippingRatesAsync(ct);
        var shippingAmount = ResolveShipping(form.ShippingMethod, summary.QualifiesForFreeShipping, standardRate, expressRate, sameDayRate);

        var total = summary.Subtotal - summary.DiscountAmount + summary.GiftWrapFee + shippingAmount;

        var order = new Order
        {
            UserId = User.GetUserId(),
            Email = form.Email.Trim(),
            Phone = form.Phone.Trim(),
            // Notification language follows the shopper's active culture so AR customers get AR mail
            // (the bilingual order templates were dead code while this was hardcoded "en").
            LanguageCode = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en",
            Currency = "KWD",
            Channel = OrderChannel.Web,
            Subtotal = summary.Subtotal,
            DiscountAmount = summary.DiscountAmount,
            GiftWrapFee = summary.GiftWrapFee,
            ShippingAmount = shippingAmount,
            TaxAmount = 0m,
            Total = total,
            DiscountCodeId = cart.DiscountCodeId,
            DiscountCodeSnapshot = cart.DiscountCode?.Code,
            GiftWrap = cart.GiftWrap,
            CustomerNote = string.IsNullOrWhiteSpace(form.Note) ? cart.Note : form.Note,
            ShipFirstName = form.FirstName.Trim(),
            ShipLastName = form.LastName.Trim(),
            ShipPhone = form.Phone.Trim(),
            ShipCountry = "KW",
            ShipGovernorate = form.Governorate.Trim(),
            ShipArea = form.Area.Trim(),
            ShipBlock = form.Block.Trim(),
            ShipStreet = form.Street.Trim(),
            ShipBuilding = form.Building.Trim(),
            ShipFloor = NullIfEmpty(form.Floor),
            ShipApartment = NullIfEmpty(form.Apartment),
            ShipDirections = NullIfEmpty(form.Directions),
            ShippingMethodName = ShippingMethodName(form.ShippingMethod)
        };

        foreach (var item in cart.Items)
        {
            var variant = item.ProductVariant;
            var product = variant.Product;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductVariantId = variant.Id,
                TitleEn = product.TitleEn,
                TitleAr = product.TitleAr,
                VariantDescription = DescribeVariant(variant),
                Sku = variant.Sku,
                ImageUrl = product.Images.PrimaryPhoto()?.Url,
                UnitPrice = variant.Price,
                Quantity = item.Quantity,
                LineTotal = variant.Price * item.Quantity
            });
        }

        // Every payment method (knet/card/applepay) goes through Tap's hosted redirect. If Tap has
        // no key configured we only fall back to a synchronous "manual" order in Development (so
        // local dev + smoke tests still complete) — see the guard below. There is no cash-on-delivery
        // option, so an unconfigured gateway must never silently book an unpaid order in production.
        var payViaTap = paymentGateway.IsConfigured;

        if (payViaTap)
        {
            // Resume the browser's in-flight Tap order (same bag + amount, still unpaid) instead of
            // spawning a new order + charge on every re-submit (back button / abandoned page).
            var tapOrder = await ResolveResumableTapOrderAsync(cart, total, ct);
            if (tapOrder is null)
            {
                order.Payments.Add(new Payment
                {
                    Provider = "Tap",
                    Method = form.PaymentMethod,
                    Status = TransactionStatus.Initiated,
                    Amount = total,
                    Currency = "KWD"
                });
                await orderService.CreateOrderAsync(order, ct);
                tapOrder = order;
            }

            // Remember the in-flight order so a re-submit resumes it. Stock is decremented and the
            // cart cleared only once payment is confirmed (TapReturn/TapWebhook), so an abandoned
            // hosted page leaves no stock impact and the customer keeps their bag to retry.
            Response.Cookies.Append(PendingPayCookie, tapOrder.OrderNumber, PendingPayCookieOptions());

            var charge = await paymentService.StartChargeForOrderAsync(
                tapOrder.Id,
                CallbackUrl(nameof(PaymentsController.TapReturn)),
                CallbackUrl(nameof(PaymentsController.TapWebhook)), ct);

            if (charge.Success && !string.IsNullOrEmpty(charge.HostedPaymentUrl))
            {
                return Redirect(charge.HostedPaymentUrl);
            }

            ModelState.AddModelError(string.Empty,
                "We couldn't start your payment. Please try again in a moment.");
            var failedVm = await BuildViewModelAsync(cart, form, ct);
            return View("Index", failedVm);
        }

        // Gateway not configured. Only Development may place a synchronous unpaid order, so local
        // dev + the smoke tests still complete; anywhere else, refuse rather than book an unpaid
        // order (there is no cash-on-delivery fallback now that online payment is the only method).
        if (!env.IsDevelopment())
        {
            ModelState.AddModelError(string.Empty,
                "Online payment is temporarily unavailable. Please try again shortly.");
            var unavailableVm = await BuildViewModelAsync(cart, form, ct);
            return View("Index", unavailableVm);
        }

        // Development-only manual fallback: confirm immediately without a real charge.
        order.Payments.Add(new Payment
        {
            Provider = "Manual",
            Method = form.PaymentMethod,
            Status = TransactionStatus.Initiated,
            Amount = total,
            Currency = "KWD"
        });
        await orderService.CreateOrderAsync(order, ct);

        // Decrement stock per line, recording an immutable adjustment row
        foreach (var item in cart.Items)
        {
            await catalogService.AdjustInventoryAsync(new InventoryAdjustment
            {
                ProductVariantId = item.ProductVariantId,
                QuantityDelta = -item.Quantity,
                Reason = InventoryAdjustmentReason.Sale,
                Note = $"Order {order.OrderNumber}"
            }, ct);
        }

        await cartService.ClearAsync(cart.Id, ct);
        Response.Cookies.Delete(PendingPayCookie);

        // Confirmation email for the synchronous (Development) order. Guarded — never blocks the redirect.
        await emailService.SendOrderConfirmationAsync(order, ct);

        TempData["LastOrderNumber"] = order.OrderNumber;
        return Redirect($"/checkout/confirmation/{order.OrderNumber}");
    }

    /// <summary>
    /// Returns the browser's in-flight Tap order (from the <c>ws_pay</c> cookie) when it is still
    /// resumable — placed-and-unpaid, with an Initiated Tap payment, owned by the same visitor, and
    /// for the exact same bag (line items + amount) — so a re-submit continues that order instead of
    /// creating a duplicate order + charge. The same-bag check matters most for guests: the ownership
    /// guard alone can't distinguish two anonymous visitors (both have a null UserId), so we additionally
    /// require the current cart to match the order's lines before re-using it on a shared device.
    /// </summary>
    private async Task<Order?> ResolveResumableTapOrderAsync(Cart cart, decimal total, CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(PendingPayCookie, out var orderNumber) || string.IsNullOrWhiteSpace(orderNumber))
            return null;

        var order = await orderService.GetByNumberAsync(orderNumber, ct);
        if (order is null || order.IsDraft) return null;
        if (order.Status != OrderStatus.Placed || order.PaymentStatus != PaymentStatus.Pending) return null;
        if (order.UserId != User.GetUserId()) return null;                  // ownership (both null for a guest)
        if (Math.Round(order.Total, 3) != Math.Round(total, 3)) return null; // the bag changed since
        if (!SameLineItems(order, cart)) return null;                        // a different bag with the same total
        if (!order.Payments.Any(p => p.Provider == "Tap" && p.Status == TransactionStatus.Initiated)) return null;

        return order;
    }

    /// <summary>True when the order's line items are exactly the current cart's (same variants and quantities).</summary>
    private static bool SameLineItems(Order order, Cart cart)
    {
        var cartLines = cart.Items
            .GroupBy(i => i.ProductVariantId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var orderLines = order.Items
            .Where(i => i.ProductVariantId is not null)
            .GroupBy(i => i.ProductVariantId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        if (cartLines.Count != orderLines.Count) return false;
        foreach (var (variantId, qty) in cartLines)
            if (!orderLines.TryGetValue(variantId, out var orderedQty) || orderedQty != qty) return false;

        return true;
    }

    /// <summary>
    /// Absolute URL for a Payments callback. Uses Tap:PublicBaseUrl when configured (so the webhook
    /// and return are always https in production, even behind a TLS-terminating proxy); otherwise
    /// falls back to the current request scheme/host.
    /// </summary>
    private string CallbackUrl(string action)
    {
        var publicBase = configuration["Tap:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(publicBase)
            ? Url.Action(action, "Payments", null, Request.Scheme)!
            : new Uri(new Uri(publicBase), Url.Action(action, "Payments")!).ToString();
    }

    private static CookieOptions PendingPayCookieOptions() => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromHours(1)
    };

    [HttpGet("checkout/confirmation")]
    public IActionResult ConfirmationIndex() => Redirect("/");

    [HttpGet("checkout/confirmation/{orderNumber}")]
    public async Task<IActionResult> Confirmation(string orderNumber, CancellationToken ct)
    {
        var order = await orderService.GetByNumberAsync(orderNumber, ct);
        if (order is null) return Redirect("/track");

        var userId = User.GetUserId();
        var isOwner = order.UserId is not null && order.UserId == userId;
        var isGuestSession = string.Equals(
            TempData.Peek("LastOrderNumber") as string, orderNumber, StringComparison.OrdinalIgnoreCase);

        if (!isOwner && !isGuestSession) return Redirect("/track");

        var crossSell = await catalogService.GetFeaturedProductsAsync(4, ct);

        var vm = new ConfirmationViewModel
        {
            Order = order,
            ShowCreateAccount = order.UserId is null && userId is null,
            CrossSell = crossSell
        };

        return View(vm);
    }

    // ------------------------------------------------------------------ helpers

    private async Task PrefillAsync(CheckoutFormModel form, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return;

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is not null)
        {
            form.Email = user.Email ?? string.Empty;
            form.Phone = user.PhoneNumber ?? string.Empty;
            form.FirstName = user.FirstName ?? string.Empty;
            form.LastName = user.LastName ?? string.Empty;
        }

        var addresses = await customerService.GetAddressesAsync(userId.Value, ct);
        var address = addresses.OrderByDescending(a => a.IsDefault).FirstOrDefault();
        if (address is null) return;

        if (!string.IsNullOrWhiteSpace(address.FirstName)) form.FirstName = address.FirstName;
        if (!string.IsNullOrWhiteSpace(address.LastName)) form.LastName = address.LastName;
        if (!string.IsNullOrWhiteSpace(address.Phone)) form.Phone = address.Phone;
        form.Governorate = address.Governorate;
        form.Area = address.Area;
        form.Block = address.Block;
        form.Street = address.Street;
        form.Building = address.Building;
        form.Floor = address.Floor;
        form.Apartment = address.Apartment;
        form.Directions = address.Directions;
    }

    private async Task<CheckoutViewModel> BuildViewModelAsync(Cart cart, CheckoutFormModel form, CancellationToken ct)
    {
        var summary = await cartService.GetSummaryAsync(cart.Id, ct);
        var (standardRate, expressRate, sameDayRate) = await LoadShippingRatesAsync(ct);

        var items = cart.Items
            .OrderBy(i => i.Id)
            .Select(i =>
            {
                var variant = i.ProductVariant;
                var product = variant.Product;
                var firstImage = product.Images.PrimaryPhoto();
                var metaParts = new[] { variant.Option2, variant.Option1 }
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .ToArray();

                return new CheckoutItemViewModel
                {
                    Title = product.TitleEn,
                    Slug = product.Slug,
                    ImageUrl = firstImage?.Url,
                    ImageAlt = firstImage?.AltEn ?? product.TitleEn,
                    VariantLabel = metaParts.Length == 0 ? null : string.Join(" · ", metaParts),
                    Quantity = i.Quantity,
                    LineTotal = variant.Price * i.Quantity
                };
            })
            .ToList();

        var methods = new List<ShippingMethodOption>
        {
            new()
            {
                Value = "standard",
                Title = "Complimentary standard",
                Meta = $"2–3 working days · free over {summary.FreeShippingThreshold:0} KWD",
                Price = summary.QualifiesForFreeShipping ? 0m : standardRate
            },
            new()
            {
                Value = "express",
                Title = "Express next-day",
                Meta = "Next working day · order by 2 PM",
                Price = expressRate
            },
            new()
            {
                Value = "same-day",
                Title = "Same-day · Kuwait City",
                Meta = "Delivered today · order by 12 PM",
                Price = sameDayRate
            }
        };

        // Make the displayed total method-aware so it matches what Place actually charges:
        // resolve the chosen method's shipping with the SAME logic POST uses, never the
        // method-blind summary.Total.
        var selectedMethod = string.IsNullOrWhiteSpace(form.ShippingMethod) ? "standard" : form.ShippingMethod;
        var selectedShipping = ResolveShipping(selectedMethod, summary.QualifiesForFreeShipping, standardRate, expressRate, sameDayRate);
        var baseTotal = summary.Subtotal - summary.DiscountAmount + summary.GiftWrapFee;

        return new CheckoutViewModel
        {
            Form = form,
            Items = items,
            Summary = summary,
            ShippingMethods = methods,
            DiscountCode = cart.DiscountCode?.Code,
            SelectedMethod = selectedMethod,
            SelectedShipping = selectedShipping,
            BaseTotal = baseTotal,
            GrandTotal = baseTotal + selectedShipping
        };
    }

    private async Task<(decimal Standard, decimal Express, decimal SameDay)> LoadShippingRatesAsync(CancellationToken ct) =>
    (
        await settingsService.GetAsync(SettingKeys.StandardShippingRate, 0m, ct),
        await settingsService.GetAsync(SettingKeys.ExpressShippingRate, 3.5m, ct),
        await settingsService.GetAsync(SettingKeys.SameDayShippingRate, 5.0m, ct)
    );

    /// <summary>
    /// The single source of truth for shipping cost by method, shared by the GET total display and
    /// the POST charge so they can never drift. Standard is free when the bag qualifies; express and
    /// same-day are always charged their rate.
    /// </summary>
    private static decimal ResolveShipping(string? method, bool qualifiesForFreeShipping, decimal standardRate, decimal expressRate, decimal sameDayRate) => method switch
    {
        "express" => expressRate,
        "same-day" => sameDayRate,
        _ => qualifiesForFreeShipping ? 0m : standardRate
    };

    private static string ShippingMethodName(string method) => method switch
    {
        "express" => "Express - next day",
        "same-day" => "Same-day - Kuwait City",
        _ => "Standard - 3-5 days"
    };

    private static string? DescribeVariant(ProductVariant variant)
    {
        var parts = new[] { variant.Option1, variant.Option2, variant.Option3 }
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" / ", parts);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
