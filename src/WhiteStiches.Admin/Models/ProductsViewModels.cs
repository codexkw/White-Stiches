using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Admin.Models;

/// <summary>Flattened category option for selects — Label carries "— " per depth level.</summary>
public record CategoryChoice(int Id, string Label);

/// <summary>One row of the admin category tree list.</summary>
public record CategoryRow(Category Category, int Depth, int ProductCount, bool HasChildren);

public class ProductListViewModel
{
    public PagedResult<Product> Products { get; init; } = new();
    public string? Search { get; init; }
    public ProductStatus? Status { get; init; }
    public int? CategoryId { get; init; }
    public IReadOnlyList<CategoryChoice> Categories { get; init; } = [];
}

/// <summary>POST /products/save form body. Field names are the exact input names.</summary>
public class ProductFormModel
{
    public int Id { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? MaterialCareEn { get; set; }
    public string? MaterialCareAr { get; set; }
    public string? SizeFitEn { get; set; }
    public string? SizeFitAr { get; set; }
    public string? SeoTitleEn { get; set; }
    public string? SeoTitleAr { get; set; }
    public string? SeoDescriptionEn { get; set; }
    public string? SeoDescriptionAr { get; set; }
    public string? Type { get; set; }
    public string? Vendor { get; set; }
    public string? Tags { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public DateTime? PublishAtUtc { get; set; }
    public bool IsFeatured { get; set; }
    public int? CategoryId { get; set; }
}

public class ProductEditViewModel
{
    public ProductFormModel Form { get; init; } = new();

    /// <summary>Null when creating; full edit graph when editing (enables image/option/variant sections).</summary>
    public Product? Product { get; init; }

    public IReadOnlyList<CategoryChoice> Categories { get; init; } = [];
}

public class ProductInventoryViewModel
{
    public Product Product { get; init; } = null!;
    public IReadOnlyList<InventoryAdjustment> History { get; init; } = [];
}

/// <summary>POST /categories/save form body. Field names are the exact input names.</summary>
public class CategoryFormModel
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>New image file to upload (optional). Replaces the existing image when present.</summary>
    public IFormFile? Image { get; set; }

    /// <summary>Existing stored image path, for display/round-trip when no new file is uploaded.</summary>
    public string? ImageUrl { get; set; }
}

public class CategoryListViewModel
{
    public IReadOnlyList<CategoryRow> Rows { get; init; } = [];
    public CategoryFormModel Form { get; init; } = new();
    public IReadOnlyList<CategoryChoice> ParentChoices { get; init; } = [];
    public bool IsEditing => Form.Id > 0;
}

/// <summary>Flattens the category hierarchy for tree lists and indented selects.</summary>
public static class CategoryTree
{
    public static IReadOnlyList<CategoryRow> BuildRows(IReadOnlyList<Category> all,
        IReadOnlyDictionary<int, int>? productCounts = null)
    {
        var ids = all.Select(c => c.Id).ToHashSet();
        // 0 = root bucket; orphans (parent missing from the set) are treated as roots.
        var byParent = all
            .GroupBy(c => c.ParentId is int pid && ids.Contains(pid) ? pid : 0)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn, StringComparer.OrdinalIgnoreCase).ToList());

        var rows = new List<CategoryRow>();

        void Walk(int parentId, int depth)
        {
            if (!byParent.TryGetValue(parentId, out var children)) return;
            foreach (var category in children)
            {
                var count = productCounts is not null && productCounts.TryGetValue(category.Id, out var n) ? n : 0;
                rows.Add(new CategoryRow(category, depth, count, byParent.ContainsKey(category.Id)));
                Walk(category.Id, depth + 1);
            }
        }

        Walk(0, 0);
        return rows;
    }

    public static IReadOnlyList<CategoryChoice> BuildChoices(IReadOnlyList<Category> all) =>
        BuildRows(all)
            .Select(r => new CategoryChoice(
                r.Category.Id,
                string.Concat(Enumerable.Repeat("— ", r.Depth)) + r.Category.NameEn))
            .ToList();

    /// <summary>Ids of a category and all of its descendants (cycle guard for ParentId selects).</summary>
    public static HashSet<int> SubtreeIds(IReadOnlyList<Category> all, int rootId)
    {
        var byParent = all.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId!.Value);
        var result = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(rootId);

        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id)) continue;
            foreach (var child in byParent[id])
            {
                stack.Push(child.Id);
            }
        }

        return result;
    }
}
