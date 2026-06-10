using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Settings;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class AuditService(WhiteStichesDbContext db) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task LogAsync(string action, Guid? userId = null, string? userName = null,
        string? entityType = null, string? entityId = null,
        object? before = null, object? after = null,
        string? ipAddress = null, CancellationToken ct = default)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            Action = action,
            UserId = userId,
            UserName = userName,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            IpAddress = ipAddress
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogEntry>> GetEntriesAsync(string? action = null, Guid? userId = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = db.AuditLog.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(e => e.Action.StartsWith(action));
        if (userId is not null) query = query.Where(e => e.UserId == userId);

        query = query.OrderByDescending(e => e.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<AuditLogEntry> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}
