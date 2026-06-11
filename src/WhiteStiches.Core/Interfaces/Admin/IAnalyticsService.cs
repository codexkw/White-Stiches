using WhiteStiches.Core.Models.Admin.Analytics;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Aggregates orders, payments, refunds, customers and stock into the advanced dashboard view
/// (Phase 1E‑1). Shared with the Reports module (Phase 1E‑4), which reuses these aggregations.
/// </summary>
public interface IAnalyticsService
{
    Task<DashboardAnalytics> GetDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
