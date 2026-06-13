using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Web.Components;

/// <summary>
/// Renders the search overlay's "Trending right now" card from a real featured product (the most
/// recently featured active piece) instead of the old hardcoded placeholder. Renders nothing when
/// the catalog has no products, in which case the idle panel simply shows one fewer column.
/// </summary>
public class SearchTrendingViewComponent(ICatalogService catalog) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var featured = await catalog.GetFeaturedProductsAsync(1, HttpContext.RequestAborted);
        return View(featured.FirstOrDefault());
    }
}
