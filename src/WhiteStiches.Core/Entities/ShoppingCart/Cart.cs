using WhiteStiches.Core.Entities.Marketing;

namespace WhiteStiches.Core.Entities.ShoppingCart;

/// <summary>Shopping cart. Guests are tracked by the Token cookie; logged-in carts attach to UserId and persist across devices.</summary>
public class Cart : BaseEntity
{
    /// <summary>Public identifier stored in the guest cart cookie. Never expose the int Id.</summary>
    public Guid Token { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    public string? Note { get; set; }
    public bool GiftWrap { get; set; }

    public int? DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}
