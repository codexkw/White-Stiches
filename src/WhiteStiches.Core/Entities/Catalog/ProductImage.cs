namespace WhiteStiches.Core.Entities.Catalog;

public class ProductImage : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public string? AltEn { get; set; }
    public string? AltAr { get; set; }

    /// <summary>When set, the PDP gallery switches to this image set when the matching color variant is selected.</summary>
    public string? ColorName { get; set; }

    public int SortOrder { get; set; }
}
