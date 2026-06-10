using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

/// <summary>Returns queue list (AD-ORD-10).</summary>
public class ReturnsQueueViewModel
{
    public PagedResult<ReturnRequest> Returns { get; init; } = new();

    /// <summary>Raw "status" query value driving the filter select ("" = all, null defaulted to Pending upstream).</summary>
    public string StatusFilter { get; init; } = string.Empty;
}

/// <summary>Return request detail with the data the action forms need.</summary>
public class ReturnDetailViewModel
{
    public ReturnRequest Request { get; init; } = null!;

    public Order Order => Request.Order;

    /// <summary>Sum of returned items' line totals (unit price × returned quantity) — default refund amount.</summary>
    public decimal SuggestedRefund { get; init; }

    /// <summary>Sum of completed refunds already on the order.</summary>
    public decimal AlreadyRefunded { get; init; }

    /// <summary>Order timeline entries related to returns/refunds, newest first.</summary>
    public IReadOnlyList<OrderEvent> ReturnEvents { get; init; } = [];
}
