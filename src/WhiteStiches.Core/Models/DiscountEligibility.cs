using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhiteStiches.Core.Models;

/// <summary>
/// Structured form of <c>DiscountCode.EligibilityJson</c>. When it has restrictions, the discount
/// applies only to cart lines whose product is selected directly or belongs to a selected collection.
/// Stored shape: <c>{"products":[1,2],"collections":[3]}</c>.
/// </summary>
public sealed class DiscountEligibility
{
    [JsonPropertyName("products")]
    public List<int> Products { get; set; } = [];

    [JsonPropertyName("collections")]
    public List<int> Collections { get; set; } = [];

    [JsonIgnore]
    public bool HasRestrictions => Products.Count > 0 || Collections.Count > 0;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Tolerant parse — returns an empty (unrestricted) eligibility on null/blank/invalid JSON.</summary>
    public static DiscountEligibility Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DiscountEligibility();
        try
        {
            var parsed = JsonSerializer.Deserialize<DiscountEligibility>(json, Options);
            if (parsed is null) return new DiscountEligibility();
            parsed.Products = parsed.Products.Where(id => id > 0).Distinct().ToList();
            parsed.Collections = parsed.Collections.Where(id => id > 0).Distinct().ToList();
            return parsed;
        }
        catch (JsonException)
        {
            return new DiscountEligibility();
        }
    }

    /// <summary>Serializes to the canonical JSON, or null when there are no restrictions (keeps the column clean).</summary>
    public string? ToJsonOrNull() =>
        HasRestrictions ? JsonSerializer.Serialize(this, Options) : null;
}

/// <summary>A cart line projected for discount eligibility: which product, and that line's money total.</summary>
public readonly record struct DiscountLineItem(int ProductId, decimal LineTotal);
