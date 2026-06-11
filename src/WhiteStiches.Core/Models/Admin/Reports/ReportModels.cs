namespace WhiteStiches.Core.Models.Admin.Reports;

/// <summary>The reports the back office can run (Phase 1E‑4).</summary>
public enum ReportType
{
    Sales = 0,
    BestSellers = 1,
    Orders = 2,
    Customers = 3,
    Inventory = 4,
    Discounts = 5,
    Payments = 6,
    Returns = 7
}

/// <summary>Filter set posted from the reports UI. Not every report honours every field —
/// <see cref="ReportMeta"/> advertises which inputs each report uses.</summary>
public sealed class ReportRequest
{
    public ReportType Type { get; init; }
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string? Status { get; init; }
    public string? Channel { get; init; }
    public string? Method { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record ReportColumn(string Label, bool Numeric);

/// <summary>A fully-formatted, paged result set — cells are already strings so the view and the
/// CSV exporter are both trivial.</summary>
public sealed class ReportTable
{
    public ReportType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<ReportColumn> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
    /// <summary>Optional summary footer (same arity as <see cref="Columns"/>).</summary>
    public IReadOnlyList<string>? TotalsRow { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

/// <summary>Describes a report for the catalog screen and tells the UI which filters to show.</summary>
public sealed record ReportMeta(
    ReportType Type,
    string Key,
    string Title,
    string Description,
    bool UsesDate,
    bool UsesStatus,
    bool UsesChannel,
    bool UsesMethod,
    bool UsesSearch);

public static class ReportCatalog
{
    public static readonly IReadOnlyList<ReportMeta> All =
    [
        new(ReportType.Sales, "sales", "Sales over time",
            "Daily orders, units, gross, discounts and net revenue.", true, false, false, false, false),
        new(ReportType.BestSellers, "best-sellers", "Best sellers",
            "Products ranked by units sold and revenue in the period.", true, false, false, false, true),
        new(ReportType.Orders, "orders", "Orders",
            "Every order with status, payment, channel and total.", true, true, true, false, true),
        new(ReportType.Customers, "customers", "Customers",
            "Customers with order counts and lifetime spend.", true, false, false, false, true),
        new(ReportType.Inventory, "inventory", "Inventory & stock value",
            "Active variants with stock, threshold and stock valuation.", false, false, false, false, true),
        new(ReportType.Discounts, "discounts", "Discounts",
            "Discount codes with usage, limits and schedule.", false, false, false, false, true),
        new(ReportType.Payments, "payments", "Payments",
            "Captured payments grouped by method and provider.", true, false, false, true, false),
        new(ReportType.Returns, "returns", "Returns",
            "Return requests with status, reason and order.", true, true, false, false, true)
    ];

    public static ReportMeta? Find(string? key) =>
        All.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));
}
