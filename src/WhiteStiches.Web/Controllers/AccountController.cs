using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Customers;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Data;
using WhiteStiches.Infrastructure.Identity;
using WhiteStiches.Web.Infrastructure;
using WhiteStiches.Web.Models.Account;

namespace WhiteStiches.Web.Controllers;

/// <summary>Customer self-service area. Login/register live in CustomerAuthController.</summary>
[Authorize]
public class AccountController(
    IOrderService orderService,
    ICustomerService customerService,
    ICartService cartService,
    ICurrentCartAccessor currentCart,
    UserManager<ApplicationUser> userManager,
    WhiteStichesDbContext db) : Controller
{
    private Guid UserId => User.GetUserId()!.Value;

    // ─── Dashboard ───────────────────────────────────────────────────────────

    [HttpGet("account")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var orders = await orderService.GetOrdersForCustomerAsync(UserId, 1, 100, ct);
        var addresses = await customerService.GetAddressesAsync(UserId, ct);
        var wishlist = await customerService.GetWishlistAsync(UserId, ct);

        var model = new DashboardViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            FirstName = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName,
            MemberSinceUtc = user.CreatedAtUtc,
            RecentOrders = orders.Items.Take(3).ToList(),
            OpenOrderCount = orders.Items.Count(o => o.Status <= OrderStatus.Shipped),
            TotalOrderCount = orders.TotalCount,
            DefaultAddress = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault(),
            WishlistPreview = wishlist.Take(4).ToList(),
            WishlistCount = wishlist.Count,
            WishlistOnSaleCount = wishlist.Count(p => p.Variants.Any(v => v.IsActive && v.CompareAtPrice > v.Price))
        };

        return View(model);
    }

    // ─── Orders ──────────────────────────────────────────────────────────────

    [HttpGet("account/orders")]
    public async Task<IActionResult> Orders(int page = 1, CancellationToken ct = default)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (page < 1) page = 1;
        var orders = await orderService.GetOrdersForCustomerAsync(UserId, page, 10, ct);

        return View(new OrdersViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Orders = orders,
            MemberSinceUtc = user.CreatedAtUtc
        });
    }

    /// <summary>Legacy demo route — the real detail page lives at /account/orders/{orderNumber}.</summary>
    [HttpGet("account/orders/detail")]
    public IActionResult OrderDetailLegacy() => RedirectToAction(nameof(Orders));

    [HttpGet("account/orders/{orderNumber}")]
    public async Task<IActionResult> OrderDetail(string orderNumber, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var order = await orderService.GetByNumberAsync(orderNumber, ct);
        if (order is null || order.UserId != UserId) return NotFound();

        return View(new OrderDetailViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Order = order
        });
    }

    // ─── Addresses ───────────────────────────────────────────────────────────

    [HttpGet("account/addresses")]
    public async Task<IActionResult> Addresses(int? edit, CancellationToken ct = default)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var addresses = await customerService.GetAddressesAsync(UserId, ct);
        Address? editAddress = null;
        if (edit is int id)
        {
            editAddress = await customerService.GetAddressAsync(UserId, id, ct);
        }

        return View(new AddressesViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Addresses = addresses,
            EditAddress = editAddress
        });
    }

    [HttpPost("account/addresses/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddressSave(AddressInputModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["AccountError"] = "Please fill in the required address fields.";
            return RedirectToAction(nameof(Addresses));
        }

        if (model.Id > 0)
        {
            var address = await customerService.GetAddressAsync(UserId, model.Id, ct);
            if (address is null) return NotFound();

            ApplyInput(address, model);
            await customerService.UpdateAddressAsync(address, ct);

            if (model.IsDefault && !address.IsDefault)
            {
                await customerService.SetDefaultAddressAsync(UserId, address.Id, ct);
            }

            TempData["AccountMessage"] = "Address updated.";
        }
        else
        {
            var address = new Address { UserId = UserId };
            ApplyInput(address, model);
            address = await customerService.AddAddressAsync(address, ct);

            if (model.IsDefault)
            {
                await customerService.SetDefaultAddressAsync(UserId, address.Id, ct);
            }

            TempData["AccountMessage"] = "Address saved.";
        }

        return RedirectToAction(nameof(Addresses));

        static void ApplyInput(Address address, AddressInputModel model)
        {
            address.Label = string.IsNullOrWhiteSpace(model.Label) ? null : model.Label.Trim();
            address.FirstName = model.FirstName.Trim();
            address.LastName = model.LastName.Trim();
            address.Phone = model.Phone?.Trim() ?? string.Empty;
            address.Country = string.IsNullOrWhiteSpace(model.Country) ? "KW" : model.Country.Trim();
            address.Governorate = model.Governorate.Trim();
            address.Area = model.Area.Trim();
            address.Block = model.Block.Trim();
            address.Street = model.Street.Trim();
            address.Building = model.Building.Trim();
            address.Floor = string.IsNullOrWhiteSpace(model.Floor) ? null : model.Floor.Trim();
            address.Apartment = string.IsNullOrWhiteSpace(model.Apartment) ? null : model.Apartment.Trim();
            address.Directions = string.IsNullOrWhiteSpace(model.Directions) ? null : model.Directions.Trim();
        }
    }

    [HttpPost("account/addresses/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddressDelete(int id, CancellationToken ct)
    {
        await customerService.DeleteAddressAsync(UserId, id, ct);
        TempData["AccountMessage"] = "Address removed.";
        return RedirectToAction(nameof(Addresses));
    }

    [HttpPost("account/addresses/default")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddressDefault(int id, CancellationToken ct)
    {
        await customerService.SetDefaultAddressAsync(UserId, id, ct);
        TempData["AccountMessage"] = "Default address updated.";
        return RedirectToAction(nameof(Addresses));
    }

    // ─── Profile ─────────────────────────────────────────────────────────────

    [HttpGet("account/profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(BuildProfileModel(user));
    }

    [HttpPost("account/profile/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileSave(ProfileInputModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
        {
            TempData["AccountError"] = "First and last name are required.";
            return RedirectToAction(nameof(Profile));
        }

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        user.PreferredLanguage = model.PreferredLanguage == "ar" ? "ar" : "en";
        if (!string.IsNullOrWhiteSpace(model.PreferredCurrency))
        {
            user.PreferredCurrency = model.PreferredCurrency.Trim();
        }
        user.MarketingEmailOptIn = model.MarketingEmailOptIn;
        user.MarketingSmsOptIn = model.MarketingSmsOptIn;
        user.MarketingWhatsAppOptIn = model.MarketingWhatsAppOptIn;

        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["AccountMessage"] = "Profile updated.";
        }
        else
        {
            TempData["AccountError"] = result.Errors.FirstOrDefault()?.Description ?? "We couldn't save your profile.";
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost("account/profile/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfilePassword(PasswordInputModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (ModelState.IsValid)
        {
            var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["AccountMessage"] = "Password changed.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View("Profile", BuildProfileModel(user));
    }

    private static ProfileViewModel BuildProfileModel(ApplicationUser user) => new()
    {
        FullName = user.FullName,
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName ?? string.Empty,
        LastName = user.LastName ?? string.Empty,
        Phone = user.PhoneNumber ?? string.Empty,
        EmailConfirmed = user.EmailConfirmed,
        PreferredLanguage = user.PreferredLanguage,
        PreferredCurrency = user.PreferredCurrency,
        MarketingEmailOptIn = user.MarketingEmailOptIn,
        MarketingSmsOptIn = user.MarketingSmsOptIn,
        MarketingWhatsAppOptIn = user.MarketingWhatsAppOptIn
    };

    // ─── Wishlist ────────────────────────────────────────────────────────────

    [HttpGet("account/wishlist")]
    public async Task<IActionResult> Wishlist(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var products = await customerService.GetWishlistAsync(UserId, ct);

        return View(new WishlistViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Products = products,
            OnSaleCount = products.Count(p => p.Variants.Any(v => v.IsActive && v.CompareAtPrice > v.Price))
        });
    }

    /// <summary>CONTRACT: consumed by product card / PDP heart buttons (Unit A).</summary>
    [HttpPost("account/wishlist/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WishlistAdd(int productId, string? returnUrl, CancellationToken ct)
    {
        await customerService.AddToWishlistAsync(UserId, productId, ct);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction(nameof(Wishlist));
    }

    [HttpPost("account/wishlist/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WishlistRemove(int productId, CancellationToken ct)
    {
        await customerService.RemoveFromWishlistAsync(UserId, productId, ct);
        TempData["AccountMessage"] = "Removed from your wishlist.";
        return RedirectToAction(nameof(Wishlist));
    }

    [HttpPost("account/wishlist/tobag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WishlistToBag(int productId, CancellationToken ct)
    {
        var products = await customerService.GetWishlistAsync(UserId, ct);
        var product = products.FirstOrDefault(p => p.Id == productId);
        if (product is null) return RedirectToAction(nameof(Wishlist));

        var variant = product.Variants
            .Where(v => v.IsActive && (v.StockQuantity > 0 || v.AllowOversell))
            .OrderBy(v => v.Position)
            .FirstOrDefault();

        if (variant is null)
        {
            TempData["AccountError"] = $"{product.TitleEn} is out of stock right now.";
            return RedirectToAction(nameof(Wishlist));
        }

        var cart = await currentCart.GetCartAsync(ct);
        await cartService.AddItemAsync(cart.Id, variant.Id, 1, ct);
        await customerService.RemoveFromWishlistAsync(UserId, productId, ct);

        TempData["AccountMessage"] = $"{product.TitleEn} moved to your bag.";
        return RedirectToAction(nameof(Wishlist));
    }

    // ─── Returns ─────────────────────────────────────────────────────────────

    [HttpGet("account/returns")]
    public async Task<IActionResult> Returns(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(await BuildReturnsModelAsync(user, newReturnOrder: null, ct));
    }

    [HttpGet("account/returns/new")]
    public async Task<IActionResult> ReturnNew(string? order, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (string.IsNullOrWhiteSpace(order)) return RedirectToAction(nameof(Returns));

        var target = await orderService.GetByNumberAsync(order, ct);
        if (target is null || target.UserId != UserId) return NotFound();

        if (target.Status != OrderStatus.Delivered)
        {
            TempData["AccountError"] = $"Order #{target.OrderNumber} isn't eligible for a return yet — only delivered orders can be returned.";
            return RedirectToAction(nameof(Returns));
        }

        return View("Returns", await BuildReturnsModelAsync(user, target, ct));
    }

    [HttpPost("account/returns/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnCreate(ReturnCreateInputModel model, CancellationToken ct)
    {
        var order = await orderService.GetByNumberAsync(model.OrderNumber, ct);
        if (order is null || order.UserId != UserId) return NotFound();

        if (order.Status != OrderStatus.Delivered)
        {
            TempData["AccountError"] = "Only delivered orders can be returned.";
            return RedirectToAction(nameof(Returns));
        }

        var orderItems = order.Items.ToDictionary(i => i.Id);
        var returnItems = new List<ReturnItem>();
        foreach (var line in model.Items)
        {
            if (line.Quantity <= 0) continue;
            if (!orderItems.TryGetValue(line.OrderItemId, out var orderItem)) continue;

            returnItems.Add(new ReturnItem
            {
                OrderItemId = orderItem.Id,
                Quantity = Math.Min(line.Quantity, orderItem.Quantity),
                Reason = string.IsNullOrWhiteSpace(line.Reason) ? null : line.Reason.Trim()
            });
        }

        if (returnItems.Count == 0)
        {
            TempData["AccountError"] = "Select at least one item to return.";
            return RedirectToAction(nameof(ReturnNew), new { order = order.OrderNumber });
        }

        var request = new ReturnRequest
        {
            OrderId = order.Id,
            UserId = UserId,
            Method = model.Method == "dropoff" ? "dropoff" : "pickup",
            CustomerReason = string.IsNullOrWhiteSpace(model.CustomerReason) ? null : model.CustomerReason.Trim(),
            Items = returnItems
        };

        request = await orderService.CreateReturnRequestAsync(request, ct);

        TempData["AccountMessage"] = $"Return {request.RmaNumber} submitted — we'll review it within 24 hours.";
        return RedirectToAction(nameof(Returns));
    }

    private async Task<ReturnsViewModel> BuildReturnsModelAsync(ApplicationUser user, Order? newReturnOrder, CancellationToken ct)
    {
        var returns = await db.ReturnRequests
            .AsNoTracking()
            .Where(r => r.UserId == UserId)
            .Include(r => r.Order)
            .Include(r => r.Items)
                .ThenInclude(i => i.OrderItem)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var orders = await orderService.GetOrdersForCustomerAsync(UserId, 1, 100, ct);

        return new ReturnsViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Returns = returns,
            EligibleOrders = orders.Items.Where(o => o.Status == OrderStatus.Delivered).ToList(),
            NewReturnOrder = newReturnOrder
        };
    }
}
