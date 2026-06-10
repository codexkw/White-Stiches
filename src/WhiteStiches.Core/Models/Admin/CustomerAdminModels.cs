using WhiteStiches.Core.Entities.Customers;
using WhiteStiches.Core.Entities.Orders;

namespace WhiteStiches.Core.Models.Admin;

/// <summary>
/// Identity-free customer DTOs for the Admin customers directory (AD-CUS-01/02).
/// Core must not reference Infrastructure, so the Identity user is flattened here.
/// </summary>
public record CustomerSummary(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string Email,
    string? Phone,
    DateTime RegisteredAtUtc,
    int OrdersCount,
    decimal TotalSpent,
    bool EmailOptIn,
    bool WhatsAppOptIn,
    bool IsLockedOut,
    DateTime? LastLoginAtUtc)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Full profile view: identity block + consent + addresses + paged order history.</summary>
public record CustomerDetail(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string Email,
    string? Phone,
    DateTime RegisteredAtUtc,
    int OrdersCount,
    decimal TotalSpent,
    bool EmailOptIn,
    bool WhatsAppOptIn,
    bool IsLockedOut,
    DateTime? LastLoginAtUtc,
    string PreferredLanguage,
    IReadOnlyList<Address> Addresses,
    PagedResult<Order> RecentOrders,
    int WishlistCount)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Result of a lockout toggle — carries before/after state for audit logging.</summary>
public record CustomerLockoutChange(
    Guid UserId,
    string? Email,
    bool WasLockedOut,
    bool IsLockedOut);
