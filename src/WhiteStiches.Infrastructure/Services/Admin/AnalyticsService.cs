using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin.Analytics;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services.Admin;

/// <summary>
/// EF-backed analytics aggregator for the advanced dashboard (Phase 1E‑1). Results are cached for
/// 60 seconds per date window so repeated dashboard loads don't re-run the ~15 aggregate queries.
/// </summary>
public sealed class AnalyticsService(WhiteStichesDbContext db, IMemoryCache cache) : IAnalyticsService
{
    /// <summary>Payment states that mean money was actually captured (vs pending/failed).</summary>
    private static readonly PaymentStatus[] Captured =
        [PaymentStatus.Paid, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded];

    public async Task<DashboardAnalytics> GetDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var key = $"dash:{fromUtc:O}:{toUtc:O}";
        if (cache.TryGetValue(key, out DashboardAnalytics? cached) && cached is not null)
            return cached;

        var length = toUtc - fromUtc;
        var prevFrom = fromUtc - length;

        var cur = await CoreAsync(fromUtc, toUtc, ct);
        var prev = await CoreAsync(prevFrom, fromUtc, ct);

        // Reusable windows for the current period.
        var paid = db.Orders.AsNoTracking().Where(o => Captured.Contains(o.PaymentStatus)
            && (o.PlacedAtUtc ?? o.CreatedAtUtc) >= fromUtc && (o.PlacedAtUtc ?? o.CreatedAtUtc) < toUtc);
        var ordersInRange = db.Orders.AsNoTracking().Where(o =>
            (o.PlacedAtUtc ?? o.CreatedAtUtc) >= fromUtc && (o.PlacedAtUtc ?? o.CreatedAtUtc) < toUtc);

        // ── Time series (revenue + orders per calendar day) ────────────────────
        var rawSeries = await paid
            .GroupBy(o => (o.PlacedAtUtc ?? o.CreatedAtUtc).Date)
            .Select(g => new { Day = g.Key, Revenue = g.Sum(o => o.Total), Orders = g.Count() })
            .ToListAsync(ct);
        var seriesMap = rawSeries.ToDictionary(x => DateOnly.FromDateTime(x.Day));
        var series = new List<TimePoint>();
        var endDay = DateOnly.FromDateTime(toUtc.AddTicks(-1));
        for (var d = DateOnly.FromDateTime(fromUtc); d <= endDay; d = d.AddDays(1))
        {
            series.Add(seriesMap.TryGetValue(d, out var v)
                ? new TimePoint(d, v.Revenue, v.Orders)
                : new TimePoint(d, 0m, 0));
        }

        // ── Payment-method mix (captured transactions) ─────────────────────────
        var methods = await db.Payments.AsNoTracking()
            .Where(p => p.Status == TransactionStatus.Captured && p.ProcessedAtUtc != null
                && p.ProcessedAtUtc >= fromUtc && p.ProcessedAtUtc < toUtc)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);
        var paymentMethods = methods
            .OrderByDescending(m => m.Amount)
            .Select(m => new MethodSlice(string.IsNullOrWhiteSpace(m.Method) ? "—" : m.Method, m.Amount, m.Count))
            .ToList();

        // ── Channel mix ────────────────────────────────────────────────────────
        var channelsRaw = await paid
            .GroupBy(o => o.Channel)
            .Select(g => new { Channel = g.Key, Orders = g.Count(), Revenue = g.Sum(o => o.Total) })
            .ToListAsync(ct);
        var channels = channelsRaw
            .OrderByDescending(c => c.Revenue)
            .Select(c => new ChannelSlice(c.Channel.ToString(), c.Orders, c.Revenue))
            .ToList();

        // ── Order-status mix ───────────────────────────────────────────────────
        var statusRaw = await ordersInRange
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var statuses = statusRaw
            .OrderByDescending(s => s.Count)
            .Select(s => new StatusSlice(s.Status.ToString(), s.Count))
            .ToList();

        // ── Top products (by revenue) ──────────────────────────────────────────
        // Order on an anonymous projection (EF can't translate OrderBy over a record ctor),
        // then map to ProductPerf in memory.
        var topProducts = (await paid
            .SelectMany(o => o.Items)
            .GroupBy(i => i.TitleEn)
            .Select(g => new { Title = g.Key, Units = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotal) })
            .OrderByDescending(x => x.Revenue)
            .Take(8)
            .ToListAsync(ct))
            .Select(x => new ProductPerf(x.Title, x.Units, x.Revenue))
            .ToList();

        // ── Top customers (by spend) ───────────────────────────────────────────
        var topCustomersRaw = await paid
            .Where(o => o.UserId != null)
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new { UserId = g.Key, Orders = g.Count(), Spent = g.Sum(o => o.Total) })
            .OrderByDescending(x => x.Spent)
            .Take(8)
            .ToListAsync(ct);
        var ids = topCustomersRaw.Select(x => x.UserId).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);
        var topCustomers = topCustomersRaw.Select(x =>
        {
            var u = users.FirstOrDefault(z => z.Id == x.UserId);
            var name = u is null ? "—" : $"{u.FirstName} {u.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(name)) name = u?.Email ?? "—";
            return new CustomerPerf(name, u?.Email ?? string.Empty, x.Orders, x.Spent);
        }).ToList();

        // ── Low stock (point-in-time) ──────────────────────────────────────────
        var lowStock = await db.ProductVariants.AsNoTracking()
            .Where(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold)
            .OrderBy(v => v.StockQuantity)
            .Select(v => new LowStockRow(v.Product.TitleEn, v.Sku, v.StockQuantity, v.LowStockThreshold))
            .Take(10)
            .ToListAsync(ct);

        // ── Operational snapshot ───────────────────────────────────────────────
        var ops = new OperationalSnapshot
        {
            PendingFulfilment = await db.Orders.CountAsync(o =>
                o.FulfillmentStatus == FulfillmentStatus.Unfulfilled
                && (o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
                && o.Status != OrderStatus.Cancelled, ct),
            PendingReturns = await db.ReturnRequests.CountAsync(r => r.Status == ReturnStatus.Pending, ct),
            AbandonedPendingPayments = await db.Orders.CountAsync(o =>
                o.PaymentStatus == PaymentStatus.Pending && o.Payments.Any(p => p.Provider == "Tap"), ct),
            UnreadMessages = await db.ContactMessages.CountAsync(m => !m.IsRead, ct),
            ActiveSubscribers = await db.NewsletterSubscribers.CountAsync(s => s.UnsubscribedAtUtc == null, ct),
            ActiveProducts = await db.Products.CountAsync(p => p.Status == ProductStatus.Active, ct),
            LowStockCount = await db.ProductVariants.CountAsync(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold, ct)
        };

        var returnsCount = await db.ReturnRequests.AsNoTracking()
            .CountAsync(r => r.CreatedAtUtc >= fromUtc && r.CreatedAtUtc < toUtc, ct);

        var curAov = cur.Orders > 0 ? cur.NetRevenue / cur.Orders : 0m;
        var prevAov = prev.Orders > 0 ? prev.NetRevenue / prev.Orders : 0m;

        var result = new DashboardAnalytics
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            RangeDays = Math.Max(1, (int)Math.Round(length.TotalDays)),
            NetRevenue = Kpi(cur.NetRevenue, prev.NetRevenue),
            Orders = Kpi(cur.Orders, prev.Orders),
            AvgOrderValue = Kpi(curAov, prevAov),
            UnitsSold = Kpi(cur.Units, prev.Units),
            NewCustomers = Kpi(cur.NewCustomers, prev.NewCustomers),
            Refunds = Kpi(cur.Refunds, prev.Refunds),
            DiscountSpend = cur.Discount,
            ReturnRatePct = cur.Orders > 0 ? (double)returnsCount / cur.Orders * 100 : 0,
            Series = series,
            PaymentMethods = paymentMethods,
            Channels = channels,
            OrderStatuses = statuses,
            TopProducts = topProducts,
            TopCustomers = topCustomers,
            LowStock = lowStock,
            Operations = ops
        };

        cache.Set(key, result, TimeSpan.FromSeconds(60));
        return result;
    }

    private async Task<CoreKpis> CoreAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var paid = db.Orders.AsNoTracking().Where(o => Captured.Contains(o.PaymentStatus)
            && (o.PlacedAtUtc ?? o.CreatedAtUtc) >= from && (o.PlacedAtUtc ?? o.CreatedAtUtc) < to);

        var capturedRevenue = await paid.SumAsync(o => (decimal?)o.Total, ct) ?? 0m;
        var paidCount = await paid.CountAsync(ct);
        var units = await paid.SelectMany(o => o.Items).SumAsync(i => (int?)i.Quantity, ct) ?? 0;
        var discount = await paid.SumAsync(o => (decimal?)o.DiscountAmount, ct) ?? 0m;

        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => r.Status == RefundStatus.Completed && r.ProcessedAtUtc != null
                && r.ProcessedAtUtc >= from && r.ProcessedAtUtc < to)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

        var newCustomers = await db.Users.AsNoTracking()
            .CountAsync(u => !u.IsStaff && u.CreatedAtUtc >= from && u.CreatedAtUtc < to, ct);

        return new CoreKpis(capturedRevenue - refunds, paidCount, units, discount, refunds, newCustomers);
    }

    private static KpiValue Kpi(decimal current, decimal previous) => new()
    {
        Current = current,
        Previous = previous,
        DeltaPct = previous == 0
            ? (current == 0 ? 0d : null)
            : (double)((current - previous) / previous) * 100d
    };

    private sealed record CoreKpis(decimal NetRevenue, int Orders, int Units, decimal Discount, decimal Refunds, int NewCustomers);
}
