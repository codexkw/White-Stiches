using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Customers;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Customer self-service data — addresses and wishlist (SF-ACC-05/06).</summary>
public interface ICustomerService
{
    Task<IReadOnlyList<Address>> GetAddressesAsync(Guid userId, CancellationToken ct = default);
    Task<Address?> GetAddressAsync(Guid userId, int addressId, CancellationToken ct = default);
    Task<Address> AddAddressAsync(Address address, CancellationToken ct = default);
    Task UpdateAddressAsync(Address address, CancellationToken ct = default);
    Task DeleteAddressAsync(Guid userId, int addressId, CancellationToken ct = default);
    Task SetDefaultAddressAsync(Guid userId, int addressId, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> GetWishlistAsync(Guid userId, CancellationToken ct = default);
    Task AddToWishlistAsync(Guid userId, int productId, CancellationToken ct = default);
    Task RemoveFromWishlistAsync(Guid userId, int productId, CancellationToken ct = default);
    Task<bool> IsInWishlistAsync(Guid userId, int productId, CancellationToken ct = default);
}
