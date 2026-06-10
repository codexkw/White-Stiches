using WhiteStiches.Core.Entities.Catalog;

namespace WhiteStiches.Core.Entities.Customers;

/// <summary>A product saved to a customer's wishlist. Guest wishlists live in a cookie and merge in at login.</summary>
public class WishlistItem : BaseEntity
{
    public Guid UserId { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
