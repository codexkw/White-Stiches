using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>Immutable stock movement record per variant — the per-variant stock history.</summary>
public class InventoryAdjustment : BaseEntity
{
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;

    /// <summary>Signed quantity change (+received / −removed).</summary>
    public int QuantityDelta { get; set; }

    public InventoryAdjustmentReason Reason { get; set; }
    public string? Note { get; set; }

    /// <summary>Staff member who made the adjustment; null for system movements (e.g., order placement).</summary>
    public Guid? StaffUserId { get; set; }
}
