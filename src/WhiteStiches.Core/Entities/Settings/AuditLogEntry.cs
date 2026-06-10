namespace WhiteStiches.Core.Entities.Settings;

/// <summary>Immutable audit log of administrative write actions (PRD Section 5 / NFR-SEC-03). Append-only.</summary>
public class AuditLogEntry : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>Action verb (e.g., "product.update", "order.refund", "settings.change").</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    public string? IpAddress { get; set; }
}
