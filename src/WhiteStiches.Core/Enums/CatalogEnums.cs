namespace WhiteStiches.Core.Enums;

public enum ProductStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}

public enum CollectionSortOrder
{
    Manual = 0,
    BestSelling = 1,
    Alphabetical = 2,
    PriceLowToHigh = 3,
    PriceHighToLow = 4,
    Newest = 5
}

public enum InventoryAdjustmentReason
{
    Received = 0,
    Correction = 1,
    Damage = 2,
    Theft = 3,
    Restock = 4,
    Sale = 5,
    ReturnRestock = 6
}
