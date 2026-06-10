using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhiteStiches.Core.Models.Admin;

/// <summary>Row DTO for the back-office collections list (AD-PRD-07).</summary>
public record CollectionListItem(
    int Id,
    string TitleEn,
    string Slug,
    bool IsSmart,
    bool IsActive,
    int ProductCount,
    DateTime UpdatedAtUtc);

/// <summary>
/// Typed form of <c>Collection.RulesJson</c>:
/// <c>{"match":"all"|"any","rules":[{"field":"tag"|"type"|"vendor"|"price","operator":"equals"|"contains"|"gt"|"lt","value":"..."}]}</c>.
/// </summary>
public class CollectionRuleSet
{
    public const string MatchAll = "all";
    public const string MatchAny = "any";

    public static readonly string[] AllowedFields = ["tag", "type", "vendor", "price"];
    public static readonly string[] AllowedOperators = ["equals", "contains", "gt", "lt"];

    [JsonPropertyName("match")]
    public string Match { get; set; } = MatchAll;

    [JsonPropertyName("rules")]
    public List<CollectionRule> Rules { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>Parses stored RulesJson; returns null when blank or malformed.</summary>
    public static CollectionRuleSet? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<CollectionRuleSet>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>A single smart-collection condition.</summary>
public class CollectionRule
{
    /// <summary>tag | type | vendor | price</summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    /// <summary>equals | contains | gt | lt</summary>
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
