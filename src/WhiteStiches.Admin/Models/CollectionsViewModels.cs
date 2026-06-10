using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Models;

public class CollectionIndexViewModel
{
    public PagedResult<CollectionListItem> Collections { get; set; } = new();
}

/// <summary>Bound by POST /collections/save (multipart; both file inputs may be empty).</summary>
public class CollectionFormViewModel
{
    public int Id { get; set; }

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Blank = auto-generated from TitleEn.</summary>
    public string? Slug { get; set; }

    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    public CollectionSortOrder SortOrder { get; set; } = CollectionSortOrder.Manual;
    public bool IsActive { get; set; }
    public bool IsSmart { get; set; }

    public string? SeoTitleEn { get; set; }
    public string? SeoTitleAr { get; set; }
    public string? SeoDescriptionEn { get; set; }
    public string? SeoDescriptionAr { get; set; }

    public IFormFile? HeroImage { get; set; }
    public IFormFile? BannerImage { get; set; }

    /// <summary>Current stored paths (display only; kept when no new file is uploaded).</summary>
    public string? ImageUrl { get; set; }
    public string? BannerUrl { get; set; }
}

/// <summary>A product currently in the collection (manual curation table).</summary>
public class CollectionProductRowViewModel
{
    public int ProductId { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Price { get; set; }
    public int Position { get; set; }
}

/// <summary>A product-picker search result (active products not yet in the collection).</summary>
public class CollectionPickerResultViewModel
{
    public int ProductId { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Price { get; set; }
}

/// <summary>One editable smart-rule row (inputs named rules[i].Field / .Operator / .Value).</summary>
public class CollectionRuleRowViewModel
{
    public string Field { get; set; } = "tag";
    public string Operator { get; set; } = "equals";
    public string Value { get; set; } = string.Empty;
}

/// <summary>Bound by POST /collections/{id}/rules (match + rows + indexed rule rows).</summary>
public class CollectionRulesFormViewModel
{
    public string Match { get; set; } = CollectionRuleSet.MatchAll;
    public int Rows { get; set; } = 1;
    public List<CollectionRuleRowViewModel> Rules { get; set; } = [];
}

public class CollectionEditViewModel
{
    public CollectionFormViewModel Form { get; set; } = new();
    public bool IsNew => Form.Id == 0;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Manual curation
    public List<CollectionProductRowViewModel> Products { get; set; } = [];
    public string? ProductSearch { get; set; }
    public bool ShowPickerResults { get; set; }
    public List<CollectionPickerResultViewModel> PickerResults { get; set; } = [];

    // Smart rules
    public CollectionRulesFormViewModel RulesForm { get; set; } = new();
    public bool HasSavedRules { get; set; }
    public int PreviewCount { get; set; }
    public List<string> PreviewTitles { get; set; } = [];
}
