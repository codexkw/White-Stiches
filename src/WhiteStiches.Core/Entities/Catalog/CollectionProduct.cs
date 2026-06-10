namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>Join entity: product membership and manual sort position within a collection. Composite key (CollectionId, ProductId).</summary>
public class CollectionProduct
{
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Position { get; set; }
}
