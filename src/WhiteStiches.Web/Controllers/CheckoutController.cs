using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Entities.ShoppingCart;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
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
    ICustomerService customerService,
    UserManager<ApplicationUser> userManager) : Controller
{
    /// <summary>Cash-on-delivery surcharge added to shipping. TODO Phase 1C: move to a settings key.</summary>
    private const decimal CodFee = 1.500m;

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

        var shippingAmount = form.ShippingMethod switch
        {
            "express" => await settingsService.GetAsync(SettingKeys.ExpressShippingRate, 3.5m, ct),
            "same-day" => await settingsService.GetAsync(SettingKeys.SameDayShippingRate, 5.0m, ct),
            _ => summary.QualifiesForFreeShipping
                ? 0m
                : await settingsService.GetAsync(SettingKeys.StandardShippingRate, 0m, ct)
        };

        if (form.PaymentMethod == "cod")
        {
            // COD fee — folded into shipping until a dedicated settings key exists
            shippingAmount += CodFee;
        }

        var total = summary.Subtotal - summary.DiscountAmount + summary.GiftWrapFee + shippingAmount;

        var order = new Order
        {
            UserId = User.GetUserId(),
            Email = form.Email.Trim(),
            Phone = form.Phone.Trim(),
            LanguageCode = "en",
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
                ImageUrl = product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
                UnitPrice = variant.Price,
                Quantity = item.Quantity,
                LineTotal = variant.Price * item.Quantity
            });
        }

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

        TempData["LastOrderNumber"] = order.OrderNumber;
        return Redirect($"/checkout/confirmation/{order.OrderNumber}");
    }

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
        var standardRate = await settingsService.GetAsync(SettingKeys.StandardShippingRate, 0m, ct);
        var expressRate = await settingsService.GetAsync(SettingKeys.ExpressShippingRate, 3.5m, ct);
        var sameDayRate = await settingsService.GetAsync(SettingKeys.SameDayShippingRate, 5.0m, ct);

        var items = cart.Items
            .OrderBy(i => i.Id)
            .Select(i =>
            {
                var variant = i.ProductVariant;
                var product = variant.Product;
                var firstImage = product.Images.OrderBy(img => img.SortOrder).FirstOrDefault();
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

        return new CheckoutViewModel
        {
            Form = form,
            Items = items,
            Summary = summary,
            ShippingMethods = methods,
            DiscountCode = cart.DiscountCode?.Code
        };
    }

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
