using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Collection curation beyond ICatalogService CRUD: manual product assignment and
/// smart-rule evaluation (AD-PRD-07). Owned by the Collections admin module.
/// </summary>
public interface ICollectionAdminService
{
    Task<PagedResult<CollectionListItem>> GetListAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>Collection with its CollectionProducts (ordered by Position) incl. product, images, and variants. Null when missing.</summary>
    Task<Collection?> GetForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Creates (Id == 0) or updates. Blank slug is generated from TitleEn; collisions get -2/-3 suffixes.</summary>
    Task<Collection> SaveAsync(Collection collection, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Active products matching the term (title/slug/type/vendor/tags) that are not yet in the collection.</summary>
    Task<IReadOnlyList<Product>> SearchProductsForPickerAsync(int collectionId, string? term, int take = 20, CancellationToken ct = default);

    /// <summary>Appends the product at the end of the manual sort. False when missing or already present.</summary>
    Task<bool> AddProductAsync(int collectionId, int productId, CancellationToken ct = default);

    Task<bool> RemoveProductAsync(int collectionId, int productId, CancellationToken ct = default);

    /// <summary>Swaps the product with its neighbour and re-sequences positions. False when at the edge or missing.</summary>
    Task<bool> MoveProductAsync(int collectionId, int productId, bool moveUp, CancellationToken ct = default);

    /// <summary>Serializes the rule set into Collection.RulesJson.</summary>
    Task<bool> SaveRulesAsync(int collectionId, CollectionRuleSet ruleSet, CancellationToken ct = default);

    /// <summary>Evaluates RulesJson against active products (price rules check any active variant). Empty when no valid rules.</summary>
    Task<IReadOnlyList<Product>> EvaluateRulesAsync(int collectionId, CancellationToken ct = default);

    /// <summary>Replaces the CollectionProducts set with the rule matches. Returns the new product count.</summary>
    Task<int> ApplyRulesAsync(int collectionId, CancellationToken ct = default);
}
