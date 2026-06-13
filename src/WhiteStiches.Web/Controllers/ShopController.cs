using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Infrastructure;
using WhiteStiches.Infrastructure.Localization;
using WhiteStiches.Web.Models.Shop;

namespace WhiteStiches.Web.Controllers;

public class ShopController(ICatalogService catalog) : Controller
{
    private const int StorefrontPageSize = 12;

    [Route("collection")]
    public async Task<IActionResult> Collection(
        string? category, string? size, string? color,
        decimal? min, decimal? max, bool instock = false,
        string? sort = null, int page = 1, CancellationToken ct = default)
    {
        category = Normalize(category);
        size = Normalize(size);
        color = Normalize(color);
        var sortKey = Normalize(sort)?.ToLowerInvariant() ?? "featured";

        var query = new ProductQuery
        {
            CategorySlug = category,
            Size = size,
            Color = color,
            PriceMin = min,
            PriceMax = max,
            InStockOnly = instock,
            Sort = MapSort(sortKey),
            Page = Math.Max(1, page),
            PageSize = StorefrontPageSize
        };

        var results = await catalog.GetProductsAsync(query, ct);
        var facets = await catalog.GetFilterFacetsAsync(query, ct);

        var categories = await catalog.GetCategoryTreeAsync(ct);
        var matched = category is null
            ? null
            : Flatten(categories).FirstOrDefault(c => string.Equals(c.Slug, category, StringComparison.OrdinalIgnoreCase));

        return View(new CollectionViewModel
        {
            Products = results,
            Categories = categories,
            BannerTitle = matched?.NameEn ?? "All",
            Category = category,
            Size = size,
            Color = color,
            Min = min,
            Max = max,
            InStock = instock,
            Sort = sortKey,
            SizeOptions = facets.Sizes,
            ColorOptions = facets.Colors
        });
    }

    [Route("collections/{slug}")]
    public async Task<IActionResult> Collections(
        string slug, string? size, string? color,
        decimal? min, decimal? max, bool instock = false,
        string? sort = null, int page = 1, CancellationToken ct = default)
    {
        var collection = await catalog.GetCollectionBySlugAsync(slug, ct);
        if (collection is null) return NotFound();

        size = Normalize(size);
        color = Normalize(color);
        var sortKey = Normalize(sort)?.ToLowerInvariant() ?? "featured";

        var query = new ProductQuery
        {
            CollectionSlug = slug,
            Size = size,
            Color = color,
            PriceMin = min,
            PriceMax = max,
            InStockOnly = instock,
            Sort = MapSort(sortKey),
            Page = Math.Max(1, page),
            PageSize = StorefrontPageSize
        };

        var results = await catalog.GetProductsAsync(query, ct);
        var facets = await catalog.GetFilterFacetsAsync(query, ct);

        var categories = await catalog.GetCategoryTreeAsync(ct);

        return View("Collection", new CollectionViewModel
        {
            Products = results,
            Categories = categories,
            BannerTitle = collection.Title(),
            CollectionSlug = slug,
            Size = size,
            Color = color,
            Min = min,
            Max = max,
            InStock = instock,
            Sort = sortKey,
            SizeOptions = facets.Sizes,
            ColorOptions = facets.Colors
        });
    }

    [Route("products/{slug}")]
    public async Task<IActionResult> ProductDetail(string slug, CancellationToken ct = default)
    {
        var product = await catalog.GetProductBySlugAsync(slug, ct);
        if (product is null || product.Status != ProductStatus.Active) return NotFound();

        // 8 related: first 4 feed "You may also like", the rest "Complete the look".
        var related = await catalog.GetRelatedProductsAsync(product.Id, 8, ct);

        return View("Product", new ProductDetailViewModel { Product = product, Related = related });
    }

    /// <summary>Legacy demo route — permanently points at the first featured product's PDP.</summary>
    [Route("product")]
    public async Task<IActionResult> ProductLegacy(CancellationToken ct = default)
    {
        var featured = await catalog.GetFeaturedProductsAsync(1, ct);
        var first = featured.FirstOrDefault();
        return first is null
            ? Redirect("/collection")
            : RedirectPermanent($"/products/{first.Slug}");
    }

    [Route("search")]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Search(string? q, int page = 1, CancellationToken ct = default)
    {
        q = Normalize(q);

        PagedResult<Product>? results = null;
        if (q is not null)
        {
            results = await catalog.GetProductsAsync(new ProductQuery
            {
                Search = q,
                Page = Math.Max(1, page),
                PageSize = StorefrontPageSize
            }, ct);
        }

        IReadOnlyList<Product> suggestions = [];
        if (results is null || results.TotalCount == 0)
        {
            suggestions = await catalog.GetFeaturedProductsAsync(4, ct);
        }

        return View(new SearchViewModel { Query = q, Results = results, Suggestions = suggestions });
    }

    /// <summary>
    /// Live product suggestions for the header search overlay. Returns a small HTML partial
    /// (injected into #searchOverlayResults by site.js), backed by the same catalog search the
    /// full /search page uses — so the overlay shows real pieces instead of placeholder cards.
    /// </summary>
    [Route("search/suggest")]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Suggest(string? q, CancellationToken ct = default)
    {
        q = Normalize(q);

        IReadOnlyList<Product> products = [];
        var total = 0;
        if (q is not null)
        {
            var results = await catalog.GetProductsAsync(new ProductQuery
            {
                Search = q,
                Page = 1,
                PageSize = 6
            }, ct);
            products = results.Items;
            total = results.TotalCount;
        }

        return PartialView("_SearchSuggest", new SearchSuggestViewModel
        {
            Query = q,
            Products = products,
            TotalCount = total
        });
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<Category> Flatten(IEnumerable<Category> categories) =>
        categories.SelectMany(c => new[] { c }.Concat(Flatten(c.Children)));

    private static ProductSort MapSort(string? sort) => sort switch
    {
        "newest" => ProductSort.Newest,
        "bestselling" => ProductSort.BestSelling,
        "price-asc" => ProductSort.PriceLowToHigh,
        "price-desc" => ProductSort.PriceHighToLow,
        "alpha" => ProductSort.Alphabetical,
        _ => ProductSort.Featured
    };
}
