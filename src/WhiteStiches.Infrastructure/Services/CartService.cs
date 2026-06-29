using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.ShoppingCart;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class CartService(WhiteStichesDbContext db, IMarketingService marketing, ISettingsService settings) : ICartService
{
    private IQueryable<Cart> CartWithItems => db.Carts
        .Include(c => c.Items)
            .ThenInclude(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
                    .ThenInclude(p => p.Images)
        .Include(c => c.DiscountCode);

    public async Task<Cart> GetOrCreateCartAsync(Guid? userId, Guid? guestToken, CancellationToken ct = default)
    {
        Cart? cart = null;

        if (userId is not null)
        {
            cart = await CartWithItems.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        }

        if (cart is null && guestToken is not null)
        {
            cart = await CartWithItems.FirstOrDefaultAsync(c => c.Token == guestToken && c.UserId == null, ct);

            // Attach a guest cart to the user on first authenticated request
            if (cart is not null && userId is not null)
            {
                cart.UserId = userId;
                await db.SaveChangesAsync(ct);
            }
        }

        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
            await db.SaveChangesAsync(ct);
        }

        return cart;
    }

    public Task<Cart?> GetCartByTokenAsync(Guid token, CancellationToken ct = default) =>
        CartWithItems.FirstOrDefaultAsync(c => c.Token == token, ct);

    public async Task<Cart> AddItemAsync(int cartId, int productVariantId, int quantity, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);

        var existing = cart.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
        if (existing is not null)
        {
            existing.Quantity += Math.Max(1, quantity);
        }
        else
        {
            db.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductVariantId = productVariantId,
                Quantity = Math.Max(1, quantity)
            });
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await RequireCartAsync(cartId, ct);
    }

    public async Task<Cart> UpdateItemQuantityAsync(int cartId, int cartItemId, int quantity, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);
        var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId);

        if (item is not null)
        {
            if (quantity <= 0)
            {
                db.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = quantity;
            }

            cart.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return cart;
    }

    public async Task<Cart> RemoveItemAsync(int cartId, int cartItemId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);
        var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId);

        if (item is not null)
        {
            db.CartItems.Remove(item);
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return cart;
    }

    public async Task ClearAsync(int cartId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);
        db.CartItems.RemoveRange(cart.Items);
        cart.DiscountCodeId = null;
        cart.Note = null;
        cart.GiftWrap = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Cart> ApplyDiscountCodeAsync(int cartId, string code, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);

        var subtotal = cart.Items.Sum(i => i.ProductVariant.Price * i.Quantity);
        var itemCount = cart.Items.Sum(i => i.Quantity);
        var lines = DiscountLines(cart);

        var validation = await marketing.ValidateDiscountCodeAsync(code, subtotal, itemCount, lines, cart.UserId, ct);
        if (!validation.IsValid || validation.Code is null)
        {
            throw new InvalidOperationException(validation.FailureReason ?? "not_found");
        }

        cart.DiscountCodeId = validation.Code.Id;
        cart.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return cart;
    }

    public async Task<Cart> RemoveDiscountCodeAsync(int cartId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);
        cart.DiscountCodeId = null;
        await db.SaveChangesAsync(ct);
        return cart;
    }

    public async Task<Cart> MergeGuestCartAsync(Guid guestToken, Guid userId, CancellationToken ct = default)
    {
        var guestCart = await CartWithItems.FirstOrDefaultAsync(c => c.Token == guestToken && c.UserId == null, ct);
        var userCart = await CartWithItems.FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (guestCart is null)
        {
            return userCart ?? await GetOrCreateCartAsync(userId, null, ct);
        }

        if (userCart is null)
        {
            guestCart.UserId = userId;
            await db.SaveChangesAsync(ct);
            return guestCart;
        }

        foreach (var guestItem in guestCart.Items.ToList())
        {
            var existing = userCart.Items.FirstOrDefault(i => i.ProductVariantId == guestItem.ProductVariantId);
            if (existing is not null)
            {
                existing.Quantity += guestItem.Quantity;
            }
            else
            {
                db.CartItems.Add(new CartItem
                {
                    CartId = userCart.Id,
                    ProductVariantId = guestItem.ProductVariantId,
                    Quantity = guestItem.Quantity
                });
            }
        }

        db.Carts.Remove(guestCart);
        userCart.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await RequireCartAsync(userCart.Id, ct);
    }

    public async Task<CartSummary> GetSummaryAsync(int cartId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);

        var subtotal = cart.Items.Sum(i => i.ProductVariant.Price * i.Quantity);
        var itemCount = cart.Items.Sum(i => i.Quantity);

        var freeShippingThreshold = await settings.GetAsync(SettingKeys.FreeShippingThreshold, 50m, ct);
        var giftWrapFee = cart.GiftWrap ? await settings.GetAsync(SettingKeys.GiftWrapFee, 3.5m, ct) : 0m;

        decimal discount = 0;
        if (cart.DiscountCode is not null)
        {
            var lines = DiscountLines(cart);
            var validation = await marketing.ValidateDiscountCodeAsync(cart.DiscountCode.Code, subtotal, itemCount, lines, cart.UserId, ct);
            discount = validation.IsValid ? validation.DiscountAmount : 0;
        }

        var shipping = subtotal >= freeShippingThreshold
            ? 0m
            : await settings.GetAsync(SettingKeys.StandardShippingRate, 0m, ct);

        return new CartSummary
        {
            ItemCount = itemCount,
            Subtotal = subtotal,
            DiscountAmount = discount,
            GiftWrapFee = giftWrapFee,
            EstimatedShipping = shipping,
            EstimatedTax = 0, // Kuwait launches at 0% VAT (LOC-06: engine stays VAT-ready)
            Total = Math.Max(0, subtotal - discount) + giftWrapFee + shipping,
            FreeShippingThreshold = freeShippingThreshold
        };
    }

    private async Task<Cart> RequireCartAsync(int cartId, CancellationToken ct) =>
        await CartWithItems.FirstOrDefaultAsync(c => c.Id == cartId, ct)
            ?? throw new InvalidOperationException($"Cart {cartId} not found.");

    /// <summary>Projects cart lines for discount eligibility: product id + that line's money total.</summary>
    private static IReadOnlyList<DiscountLineItem> DiscountLines(Cart cart) =>
        cart.Items
            .Select(i => new DiscountLineItem(i.ProductVariant.ProductId, i.ProductVariant.Price * i.Quantity))
            .ToList();
}
