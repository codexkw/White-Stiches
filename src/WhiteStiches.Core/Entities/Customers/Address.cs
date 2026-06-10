namespace WhiteStiches.Core.Entities.Customers;

/// <summary>Saved customer address using the Kuwait address structure (governorate / area / block / street / building).</summary>
public class Address : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Optional label shown on the address card (e.g., "Home", "Office").</summary>
    public string? Label { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    /// <summary>ISO country code; Phase 1 is Kuwait-only ("KW").</summary>
    public string Country { get; set; } = "KW";

    public string Governorate { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Block { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Directions { get; set; }

    public bool IsDefault { get; set; }
}
