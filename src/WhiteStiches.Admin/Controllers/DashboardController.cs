using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces.Admin;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Advanced analytics dashboard (Phase 1E‑1): KPIs with period-over-period deltas,
/// a revenue/orders time series, breakdowns, leaderboards and an operational snapshot.</summary>
public class DashboardController(IAnalyticsService analytics) : Controller
{
    private static readonly int[] AllowedRanges = [7, 14, 30, 90, 365];

    public async Task<IActionResult> Index(int days = 30, CancellationToken ct = default)
    {
        if (!AllowedRanges.Contains(days)) days = 30;

        ViewData["Title"] = "Dashboard";
        ViewData["Nav"] = "dashboard";
        ViewData["RangeDays"] = days;

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-days);

        var model = await analytics.GetDashboardAsync(fromUtc, toUtc, ct);
        return View(model);
    }
}
