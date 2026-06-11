using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin.Reports;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services.Admin;

/// <summary>
/// Builds the back office's filterable/exportable reports (Phase 1E‑4). Each report is returned as a
/// fully-formatted <see cref="ReportTable"/>. Grouped reports are bounded by <see cref="MaxRows"/>.
/// </summary>
public sealed class ReportService(WhiteStichesDbContext db) : IReportService
{
    private const int MaxRows = 20_000;

    private static readonly PaymentStatus[] Captured =
        [PaymentStatus.Paid, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded];

    public Task<ReportTable> BuildAsync(ReportRequest req, CancellationToken ct = default) => req.Type switch
    {
        ReportType.Sales => SalesAsync(req, ct),
        ReportType.BestSellers => BestSellersAsync(req, ct),
        ReportType.Orders => OrdersAsync(req, ct),
        ReportType.Customers => CustomersAsync(req, ct),
        ReportType.Inventory => InventoryAsync(req, ct),
        ReportType.Discounts => DiscountsAsync(req, ct),
        ReportType.Payments => PaymentsAsync(req, ct),
        ReportType.Returns => ReturnsAsync(req, ct),
        _ => SalesAsync(req, ct)
    };

    // ─────────────────────────────────────────────────────────────── helpers
    private static string M(decimal d) => d.ToString("#,##0.000");
    private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

    private IQueryable<Order> PaidInRange(ReportRequest r) => db.Orders.AsNoTracking()
        .Where(o => Captured.Contains(o.PaymentStatus)
            && (o.PlacedAtUtc ?? o.CreatedAtUtc) >= r.FromUtc && (o.PlacedAtUtc ?? o.CreatedAtUtc) < r.ToUtc);

    private IQueryable<Order> OrdersInRange(ReportRequest r) => db.Orders.AsNoTracking()
        .Where(o => (o.PlacedAtUtc ?? o.CreatedAtUtc) >= r.FromUtc && (o.PlacedAtUtc ?? o.CreatedAtUtc) < r.ToUtc);

    private static ReportTable Paged(ReportType type, string title, List<ReportColumn> cols,
        List<IReadOnlyList<string>> all, IReadOnlyList<string>? totals, ReportRequest req)
    {
        var paged = all.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).ToList();
        return new ReportTable
        {
            Type = type, Title = title, Columns = cols, Rows = paged, TotalsRow = totals,
            TotalCount = all.Count, Page = req.Page, PageSize = req.PageSize
        };
    }

    // ─────────────────────────────────────────────────────────────── reports

    private async Task<ReportTable> SalesAsync(ReportRequest req, CancellationToken ct)
    {
        var rows = await PaidInRange(req)
            .GroupBy(o => (o.PlacedAtUtc ?? o.CreatedAtUtc).Date)
            .Select(g => new { Day = g.Key, Orders = g.Count(), Revenue = g.Sum(o => o.Total), Discount = g.Sum(o => o.DiscountAmount) })
            .OrderBy(x => x.Day)
            .ToListAsync(ct);

        var cols = new List<ReportColumn> { new("Date", false), new("Orders", true), new("Revenue (KWD)", true), new("Discounts (KWD)", true) };
        var all = rows.Select(r => (IReadOnlyList<string>)[D(r.Day), r.Orders.ToString(), M(r.Revenue), M(r.Discount)]).ToList();
        IReadOnlyList<string> totals = ["Total", rows.Sum(r => r.Orders).ToString(), M(rows.Sum(r => r.Revenue)), M(rows.Sum(r => r.Discount))];
        return Paged(ReportType.Sales, "Sales over time", cols, all, totals, req);
    }

    private async Task<ReportTable> BestSellersAsync(ReportRequest req, CancellationToken ct)
    {
        var items = PaidInRange(req).SelectMany(o => o.Items);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            items = items.Where(i => i.TitleEn.Contains(s) || i.TitleAr.Contains(s));
        }

        var rows = await items
            .GroupBy(i => i.TitleEn)
            .Select(g => new { Title = g.Key, Units = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotal) })
            .OrderByDescending(x => x.Revenue)
            .Take(MaxRows)
            .ToListAsync(ct);

        var cols = new List<ReportColumn> { new("Product", false), new("Units", true), new("Revenue (KWD)", true) };
        var all = rows.Select(r => (IReadOnlyList<string>)[r.Title, r.Units.ToString(), M(r.Revenue)]).ToList();
        IReadOnlyList<string> totals = ["Total", rows.Sum(r => r.Units).ToString(), M(rows.Sum(r => r.Revenue))];
        return Paged(ReportType.BestSellers, "Best sellers", cols, all, totals, req);
    }

    private async Task<ReportTable> OrdersAsync(ReportRequest req, CancellationToken ct)
    {
        var q = OrdersInRange(req);
        if (Enum.TryParse<OrderStatus>(req.Status, true, out var st)) q = q.Where(o => o.Status == st);
        if (Enum.TryParse<OrderChannel>(req.Channel, true, out var ch)) q = q.Where(o => o.Channel == ch);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s) || o.Email.Contains(s)
                || (o.ShipFirstName + " " + o.ShipLastName).Contains(s));
        }

        q = q.OrderByDescending(o => o.PlacedAtUtc ?? o.CreatedAtUtc);
        var total = await q.CountAsync(ct);
        var page = await q.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .Select(o => new { o.OrderNumber, Date = o.PlacedAtUtc ?? o.CreatedAtUtc, o.Status, o.PaymentStatus, o.Channel, o.Total })
            .ToListAsync(ct);

        var cols = new List<ReportColumn>
        {
            new("Order", false), new("Date", false), new("Status", false),
            new("Payment", false), new("Channel", false), new("Total (KWD)", true)
        };
        var rows = page.Select(o => (IReadOnlyList<string>)
            [o.OrderNumber, D(o.Date), o.Status.ToString(), o.PaymentStatus.ToString(), o.Channel.ToString(), M(o.Total)]).ToList();

        return new ReportTable
        {
            Type = ReportType.Orders, Title = "Orders", Columns = cols, Rows = rows,
            TotalCount = total, Page = req.Page, PageSize = req.PageSize
        };
    }

    private async Task<ReportTable> CustomersAsync(ReportRequest req, CancellationToken ct)
    {
        var grouped = await PaidInRange(req).Where(o => o.UserId != null)
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new { Id = g.Key, Orders = g.Count(), Spent = g.Sum(o => o.Total), Last = g.Max(o => o.PlacedAtUtc ?? o.CreatedAtUtc) })
            .OrderByDescending(x => x.Spent)
            .Take(MaxRows)
            .ToListAsync(ct);

        var ids = grouped.Select(x => x.Id).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email }).ToListAsync(ct);

        var search = req.Search?.Trim();
        var joined = grouped.Select(g =>
        {
            var u = users.FirstOrDefault(z => z.Id == g.Id);
            var name = $"{u?.FirstName} {u?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(name)) name = u?.Email ?? "—";
            return new { Name = name, Email = u?.Email ?? string.Empty, g.Orders, g.Spent, g.Last };
        });
        if (!string.IsNullOrWhiteSpace(search))
            joined = joined.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        var list = joined.ToList();

        var cols = new List<ReportColumn>
        {
            new("Customer", false), new("Email", false), new("Orders", true), new("Spent (KWD)", true), new("Last order", false)
        };
        var all = list.Select(x => (IReadOnlyList<string>)[x.Name, x.Email, x.Orders.ToString(), M(x.Spent), D(x.Last)]).ToList();
        IReadOnlyList<string> totals = ["Total", $"{list.Count} customers", list.Sum(x => x.Orders).ToString(), M(list.Sum(x => x.Spent)), ""];
        return Paged(ReportType.Customers, "Customers", cols, all, totals, req);
    }

    private async Task<ReportTable> InventoryAsync(ReportRequest req, CancellationToken ct)
    {
        var q = db.ProductVariants.AsNoTracking().Where(v => v.IsActive);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(v => v.Product.TitleEn.Contains(s) || (v.Sku != null && v.Sku.Contains(s)));
        }

        q = q.OrderBy(v => v.Product.TitleEn).ThenBy(v => v.Sku);
        var total = await q.CountAsync(ct);
        var totalValue = await q.SumAsync(v => (decimal?)(v.StockQuantity * (v.Cost ?? 0m)), ct) ?? 0m;

        var page = await q.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .Select(v => new { Product = v.Product.TitleEn, v.Sku, v.StockQuantity, v.LowStockThreshold, Cost = v.Cost ?? 0m, Value = v.StockQuantity * (v.Cost ?? 0m) })
            .ToListAsync(ct);

        var cols = new List<ReportColumn>
        {
            new("Product", false), new("SKU", false), new("Stock", true),
            new("Threshold", true), new("Cost (KWD)", true), new("Stock value (KWD)", true)
        };
        var rows = page.Select(v => (IReadOnlyList<string>)
            [v.Product, v.Sku ?? "—", v.StockQuantity.ToString(), v.LowStockThreshold.ToString(), M(v.Cost), M(v.Value)]).ToList();
        IReadOnlyList<string> totals = ["Total", "", "", "", "", M(totalValue)];

        return new ReportTable
        {
            Type = ReportType.Inventory, Title = "Inventory & stock value", Columns = cols, Rows = rows,
            TotalsRow = totals, TotalCount = total, Page = req.Page, PageSize = req.PageSize
        };
    }

    private async Task<ReportTable> DiscountsAsync(ReportRequest req, CancellationToken ct)
    {
        var q = db.DiscountCodes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(d => d.Code.Contains(s));
        }

        q = q.OrderByDescending(d => d.TimesUsed);
        var total = await q.CountAsync(ct);
        var page = await q.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .Select(d => new { d.Code, d.Type, d.Value, d.TimesUsed, d.UsageLimitTotal, d.IsActive, d.StartsAtUtc, d.EndsAtUtc })
            .ToListAsync(ct);

        var cols = new List<ReportColumn>
        {
            new("Code", false), new("Type", false), new("Value", true), new("Used", true),
            new("Limit", true), new("Active", false), new("Window", false)
        };
        var rows = page.Select(d => (IReadOnlyList<string>)
        [
            d.Code, d.Type.ToString(), d.Value.ToString("#,##0.###"), d.TimesUsed.ToString(),
            d.UsageLimitTotal?.ToString() ?? "∞", d.IsActive ? "Yes" : "No",
            $"{(d.StartsAtUtc.HasValue ? D(d.StartsAtUtc.Value) : "—")} → {(d.EndsAtUtc.HasValue ? D(d.EndsAtUtc.Value) : "—")}"
        ]).ToList();

        return new ReportTable
        {
            Type = ReportType.Discounts, Title = "Discounts", Columns = cols, Rows = rows,
            TotalCount = total, Page = req.Page, PageSize = req.PageSize
        };
    }

    private async Task<ReportTable> PaymentsAsync(ReportRequest req, CancellationToken ct)
    {
        var q = db.Payments.AsNoTracking().Where(p => p.Status == TransactionStatus.Captured
            && p.ProcessedAtUtc != null && p.ProcessedAtUtc >= req.FromUtc && p.ProcessedAtUtc < req.ToUtc);
        if (!string.IsNullOrWhiteSpace(req.Method))
        {
            var m = req.Method.Trim();
            q = q.Where(p => p.Method == m);
        }

        var rows = await q
            .GroupBy(p => new { p.Method, p.Provider })
            .Select(g => new { g.Key.Method, g.Key.Provider, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);

        var cols = new List<ReportColumn>
        {
            new("Method", false), new("Provider", false), new("Transactions", true), new("Captured (KWD)", true)
        };
        var all = rows.Select(r => (IReadOnlyList<string>)
            [string.IsNullOrWhiteSpace(r.Method) ? "—" : r.Method, r.Provider, r.Count.ToString(), M(r.Amount)]).ToList();
        IReadOnlyList<string> totals = ["Total", "", rows.Sum(r => r.Count).ToString(), M(rows.Sum(r => r.Amount))];
        return Paged(ReportType.Payments, "Payments", cols, all, totals, req);
    }

    private async Task<ReportTable> ReturnsAsync(ReportRequest req, CancellationToken ct)
    {
        var q = db.ReturnRequests.AsNoTracking().Include(r => r.Order)
            .Where(r => r.CreatedAtUtc >= req.FromUtc && r.CreatedAtUtc < req.ToUtc);
        if (Enum.TryParse<ReturnStatus>(req.Status, true, out var st)) q = q.Where(r => r.Status == st);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(r => r.RmaNumber.Contains(s) || r.Order.OrderNumber.Contains(s));
        }

        q = q.OrderByDescending(r => r.CreatedAtUtc);
        var total = await q.CountAsync(ct);
        var page = await q.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .Select(r => new { r.RmaNumber, Order = r.Order.OrderNumber, r.Status, r.CustomerReason, r.CreatedAtUtc })
            .ToListAsync(ct);

        var cols = new List<ReportColumn>
        {
            new("RMA", false), new("Order", false), new("Status", false), new("Reason", false), new("Created", false)
        };
        var rows = page.Select(r => (IReadOnlyList<string>)
            [r.RmaNumber, r.Order, r.Status.ToString(), string.IsNullOrWhiteSpace(r.CustomerReason) ? "—" : r.CustomerReason, D(r.CreatedAtUtc)]).ToList();

        return new ReportTable
        {
            Type = ReportType.Returns, Title = "Returns", Columns = cols, Rows = rows,
            TotalCount = total, Page = req.Page, PageSize = req.PageSize
        };
    }
}
