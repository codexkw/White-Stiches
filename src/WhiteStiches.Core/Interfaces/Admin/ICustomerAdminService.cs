using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Customer directory: search/list with order aggregates, profile detail, consent
/// status (AD-CUS-01/02). Owned by the Customers admin module.
/// </summary>
public interface ICustomerAdminService
{
    /// <summary>
    /// Paged customer list — only users holding no staff role count as customers.
    /// <paramref name="search"/> matches name, email, or phone.
    /// </summary>
    Task<PagedResult<CustomerSummary>> SearchAsync(string? search = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>Full profile (identity, consent, addresses, paged order history, wishlist count); null when not a customer.</summary>
    Task<CustomerDetail?> GetDetailAsync(Guid userId,
        int ordersPage = 1, int ordersPageSize = 10, CancellationToken ct = default);

    /// <summary>
    /// Toggles account lockout (lock = far-future lockout end, unlock = cleared).
    /// Returns null when the user does not exist or is staff (staff lockout belongs to the Staff module).
    /// </summary>
    Task<CustomerLockoutChange?> SetLockoutAsync(Guid userId, bool locked, CancellationToken ct = default);
}
