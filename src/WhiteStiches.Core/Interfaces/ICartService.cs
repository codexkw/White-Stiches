using WhiteStiches.Core.Entities.ShoppingCart;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Cart lifecycle — guest carts by token cookie, customer carts by user id, merged at login (SF-CRT-06).</summary>
public interface ICartService
{
    Task<Cart> GetOrCreateCartAsync(Guid? userId, Guid? guestToken, CancellationToken ct = default);
    Task<Cart?> GetCartByTokenAsync(Guid token, CancellationToken ct = default);

    Task<Cart> AddItemAsync(int cartId, int productVariantId, int quantity, CancellationToken ct = default);
    Task<Cart> UpdateItemQuantityAsync(int cartId, int cartItemId, int quantity, CancellationToken ct = default);
    Task<Cart> RemoveItemAsync(int cartId, int cartItemId, CancellationToken ct = default);
    Task ClearAsync(int cartId, CancellationToken ct = default);

    Task<Cart> ApplyDiscountCodeAsync(int cartId, string code, CancellationToken ct = default);
    Task<Cart> RemoveDiscountCodeAsync(int cartId, CancellationToken ct = default);

    /// <summary>Merges a guest cart into the customer cart at login, then deletes the guest cart.</summary>
    Task<Cart> MergeGuestCartAsync(Guid guestToken, Guid userId, CancellationToken ct = default);

    Task<CartSummary> GetSummaryAsync(int cartId, CancellationToken ct = default);
}
