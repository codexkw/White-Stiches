using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Customers;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class CustomerService(WhiteStichesDbContext db) : ICustomerService
{
    public async Task<IReadOnlyList<Address>> GetAddressesAsync(Guid userId, CancellationToken ct = default) =>
        await db.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<Address?> GetAddressAsync(Guid userId, int addressId, CancellationToken ct = default) =>
        db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct);

    public async Task<Address> AddAddressAsync(Address address, CancellationToken ct = default)
    {
        var hasAny = await db.Addresses.AnyAsync(a => a.UserId == address.UserId, ct);
        if (!hasAny)
        {
            address.IsDefault = true;
        }
        else if (address.IsDefault)
        {
            await ClearDefaultAsync(address.UserId, ct);
        }

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);
        return address;
    }

    public async Task UpdateAddressAsync(Address address, CancellationToken ct = default)
    {
        if (address.IsDefault)
        {
            await ClearDefaultAsync(address.UserId, ct, exceptId: address.Id);
        }

        db.Addresses.Update(address);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAddressAsync(Guid userId, int addressId, CancellationToken ct = default)
    {
        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct);
        if (address is null) return;

        db.Addresses.Remove(address);
        await db.SaveChangesAsync(ct);

        // Keep exactly one default when addresses remain
        if (address.IsDefault)
        {
            var next = await db.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (next is not null)
            {
                next.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task SetDefaultAddressAsync(Guid userId, int addressId, CancellationToken ct = default)
    {
        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct)
            ?? throw new InvalidOperationException($"Address {addressId} not found for user.");

        await ClearDefaultAsync(userId, ct, exceptId: addressId);
        address.IsDefault = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Product>> GetWishlistAsync(Guid userId, CancellationToken ct = default)
    {
        // Include cannot follow Select — fetch ids first, then load products with their graphs.
        var productIds = await db.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(w => w.ProductId)
            .ToListAsync(ct);

        if (productIds.Count == 0) return [];

        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Variants.Where(v => v.IsActive))
            .ToDictionaryAsync(p => p.Id, ct);

        return productIds
            .Where(products.ContainsKey)
            .Select(id => products[id])
            .ToList();
    }

    public async Task AddToWishlistAsync(Guid userId, int productId, CancellationToken ct = default)
    {
        var exists = await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (exists) return;

        db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveFromWishlistAsync(Guid userId, int productId, CancellationToken ct = default)
    {
        var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (item is null) return;

        db.WishlistItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsInWishlistAsync(Guid userId, int productId, CancellationToken ct = default) =>
        db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct);

    private async Task ClearDefaultAsync(Guid userId, CancellationToken ct, int? exceptId = null)
    {
        var defaults = await db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault && (exceptId == null || a.Id != exceptId))
            .ToListAsync(ct);

        foreach (var d in defaults)
        {
            d.IsDefault = false;
        }
    }
}
