namespace WhiteStiches.Core.Models;

/// <summary>Computed cart totals for rendering the summary panel and mini-cart (SF-CRT-03).</summary>
public class CartSummary
{
    public int ItemCount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GiftWrapFee { get; set; }
    public decimal EstimatedShipping { get; set; }
    public decimal EstimatedTax { get; set; }
    public decimal Total { get; set; }

    public decimal FreeShippingThreshold { get; set; }
    public decimal AmountToFreeShipping => Math.Max(0, FreeShippingThreshold - Subtotal);
    public bool QualifiesForFreeShipping => Subtotal >= FreeShippingThreshold;
}
