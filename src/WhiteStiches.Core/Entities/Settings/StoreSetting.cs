namespace WhiteStiches.Core.Entities.Settings;

/// <summary>Key-value store configuration (store details, thresholds, integration toggles).</summary>
public class StoreSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }

    /// <summary>Logical group for the Admin settings UI ("store", "shipping", "payments", "notifications"...).</summary>
    public string? Group { get; set; }
}
