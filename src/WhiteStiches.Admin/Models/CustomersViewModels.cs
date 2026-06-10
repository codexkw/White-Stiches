using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Models;

/// <summary>List screen: paged customer directory + search filter (AD-CUS-01).</summary>
public class CustomerListViewModel
{
    public required PagedResult<CustomerSummary> Customers { get; init; }

    /// <summary>Search across name / email / phone — bound to the "q" query param.</summary>
    public string? Search { get; init; }
}

/// <summary>Profile screen: identity, consent, addresses, paged order history (AD-CUS-02).</summary>
public class CustomerDetailViewModel
{
    public required CustomerDetail Customer { get; init; }
}
