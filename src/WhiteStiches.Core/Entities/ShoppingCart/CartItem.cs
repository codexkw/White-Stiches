using WhiteStiches.Core.Entities.Catalog;

namespace WhiteStiches.Core.Entities.ShoppingCart;

public class CartItem : BaseEntity
{
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;

    public int Quantity { get; set; } = 1;
}
