namespace WhiteStiches.Core.Models.Admin;

/// <summary>
/// Editable fields of a draft order (AD-ORD-08): manual shipping/discount amounts,
/// customer note, contact, and shipping address snapshot. All address fields are
/// optional while the order remains a draft. Owned by the Orders admin module.
/// </summary>
public record DraftOrderUpdate(
    decimal ShippingAmount,
    decimal DiscountAmount,
    string? CustomerNote = null,
    string? Email = null,
    string? Phone = null,
    string? ShipFirstName = null,
    string? ShipLastName = null,
    string? ShipPhone = null,
    string? ShipGovernorate = null,
    string? ShipArea = null,
    string? ShipBlock = null,
    string? ShipStreet = null,
    string? ShipBuilding = null,
    string? ShipFloor = null,
    string? ShipApartment = null,
    string? ShipDirections = null);
