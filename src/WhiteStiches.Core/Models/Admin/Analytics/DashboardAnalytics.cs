namespace WhiteStiches.Core.Models.Admin.Analytics;

/// <summary>
/// Everything the advanced admin dashboard renders for a chosen date window (Phase 1E‑1):
/// period-over-period KPIs, a revenue/orders time series, breakdowns, leaderboards, and a
/// point-in-time operational snapshot. Money is KWD (3 decimals).
/// </summary>
public sealed class DashboardAnalytics
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public int RangeDays { get; init; }

    public KpiValue NetRevenue { get; init; } = new();
    public KpiValue Orders { get; init; } = new();
    public KpiValue AvgOrderValue { get; init; } = new();
    public KpiValue UnitsSold { get; init; } = new();
    public KpiValue NewCustomers { get; init; } = new();
    public KpiValue Refunds { get; init; } = new();

    public decimal DiscountSpend { get; init; }
    public double ReturnRatePct { get; init; }

    public IReadOnlyList<TimePoint> Series { get; init; } = [];
    public IReadOnlyList<MethodSlice> PaymentMethods { get; init; } = [];
    public IReadOnlyList<ChannelSlice> Channels { get; init; } = [];
    public IReadOnlyList<StatusSlice> OrderStatuses { get; init; } = [];
    public IReadOnlyList<ProductPerf> TopProducts { get; init; } = [];
    public IReadOnlyList<CustomerPerf> TopCustomers { get; init; } = [];
    public IReadOnlyList<LowStockRow> LowStock { get; init; } = [];
    public OperationalSnapshot Operations { get; init; } = new();
}

/// <summary>A single metric with its value, the preceding-period value, and the % change.</summary>
public sealed class KpiValue
{
    public decimal Current { get; init; }
    public decimal Previous { get; init; }
    /// <summary>Percent change vs the preceding equal period; null when there is no baseline.</summary>
    public double? DeltaPct { get; init; }
    public bool IsUp => DeltaPct is >= 0;
}

public sealed record TimePoint(DateOnly Date, decimal Revenue, int Orders);
public sealed record MethodSlice(string Method, decimal Amount, int Count);
public sealed record ChannelSlice(string Channel, int Orders, decimal Revenue);
public sealed record StatusSlice(string Status, int Count);
public sealed record ProductPerf(string Title, int Units, decimal Revenue);
public sealed record CustomerPerf(string Name, string Email, int Orders, decimal Spent);
public sealed record LowStockRow(string Product, string? Variant, int Stock, int Threshold);

/// <summary>Point-in-time counts that need staff attention now (not bound to the date window).</summary>
public sealed class OperationalSnapshot
{
    public int PendingFulfilment { get; init; }
    public int PendingReturns { get; init; }
    public int AbandonedPendingPayments { get; init; }
    public int UnreadMessages { get; init; }
    public int ActiveSubscribers { get; init; }
    public int ActiveProducts { get; init; }
    public int LowStockCount { get; init; }
}
