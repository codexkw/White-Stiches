using WhiteStiches.Core.Entities.Settings;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Append-only audit logging for every administrative write action (PRD Section 5).</summary>
public interface IAuditService
{
    Task LogAsync(string action, Guid? userId = null, string? userName = null,
        string? entityType = null, string? entityId = null,
        object? before = null, object? after = null,
        string? ipAddress = null, CancellationToken ct = default);

    Task<PagedResult<AuditLogEntry>> GetEntriesAsync(string? action = null, Guid? userId = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default);
}
