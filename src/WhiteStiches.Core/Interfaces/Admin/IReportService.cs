using WhiteStiches.Core.Models.Admin.Reports;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Builds the back office's filterable, exportable reports (Phase 1E‑4). Every report is returned
/// as a fully-formatted <see cref="ReportTable"/> so one view and one CSV exporter serve them all.
/// </summary>
public interface IReportService
{
    Task<ReportTable> BuildAsync(ReportRequest request, CancellationToken ct = default);
}
