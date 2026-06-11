using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin.Reports;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Reports module (Phase 1E‑4): a catalog of filterable reports, each rendered through one
/// generic table view and exportable to CSV.</summary>
[Route("reports")]
public class ReportsController(IReportService reports, IAuditService audit) : Controller
{
    private const int PageSize = 50;

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        ViewData["Nav"] = "reports";
        return View(ReportCatalog.All);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Show(string key, string? from, string? to, string? status,
        string? channel, string? method, string? search, int page = 1, CancellationToken ct = default)
    {
        var meta = ReportCatalog.Find(key);
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Nav"] = "reports";

        var req = BuildRequest(meta, from, to, status, channel, method, search, page, PageSize);
        var table = await reports.BuildAsync(req, ct);

        ViewBag.Meta = meta;
        ViewBag.FromStr = (ParseDate(from) ?? DateTime.UtcNow.Date.AddDays(-30)).ToString("yyyy-MM-dd");
        ViewBag.ToStr = (ParseDate(to) ?? DateTime.UtcNow.Date).ToString("yyyy-MM-dd");
        ViewBag.Status = status;
        ViewBag.Channel = channel;
        ViewBag.Method = method;
        ViewBag.Search = search;
        return View("Table", table);
    }

    [HttpGet("{key}/export")]
    public async Task<IActionResult> Export(string key, string? from, string? to, string? status,
        string? channel, string? method, string? search, CancellationToken ct = default)
    {
        var meta = ReportCatalog.Find(key);
        if (meta is null) return NotFound();

        var req = BuildRequest(meta, from, to, status, channel, method, search, 1, 100_000);
        var table = await reports.BuildAsync(req, ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", table.Columns.Select(c => Csv(c.Label))));
        foreach (var row in table.Rows)
            sb.AppendLine(string.Join(",", row.Select(Csv)));
        if (table.TotalsRow is not null)
            sb.AppendLine(string.Join(",", table.TotalsRow.Select(Csv)));

        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        await audit.LogAsync("report.export", userId, User.Identity?.Name, "Report",
            after: new { meta.Key, rows = table.Rows.Count }, ct: ct);

        // UTF-8 BOM so Excel opens Arabic cells correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"{meta.Key}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    private static ReportRequest BuildRequest(ReportMeta meta, string? from, string? to, string? status,
        string? channel, string? method, string? search, int page, int pageSize)
    {
        var toDate = ParseDate(to) ?? DateTime.UtcNow.Date;
        var fromDate = ParseDate(from) ?? toDate.AddDays(-30);

        return new ReportRequest
        {
            Type = meta.Type,
            // Inclusive end day; non-date reports span everything.
            FromUtc = meta.UsesDate ? fromDate : DateTime.UtcNow.AddYears(-50),
            ToUtc = meta.UsesDate ? toDate.AddDays(1) : DateTime.UtcNow.AddDays(1),
            Status = meta.UsesStatus ? status : null,
            Channel = meta.UsesChannel ? channel : null,
            Method = meta.UsesMethod ? method : null,
            Search = meta.UsesSearch ? search : null,
            Page = page < 1 ? 1 : page,
            PageSize = pageSize
        };
    }

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? d.Date : null;

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
