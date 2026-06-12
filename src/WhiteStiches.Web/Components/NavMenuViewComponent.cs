using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Models.Nav;

namespace WhiteStiches.Web.Components;

/// <summary>
/// Renders a navigation surface from the live catalog (categories + collections).
/// Invoked once per surface — <c>HeaderNav</c>, <c>MegaMenu</c>, <c>Drawer</c>,
/// <c>Footer</c>, <c>Circles</c> — each resolving its own named view. The catalog
/// reads are memoized in <see cref="HttpContext.Items"/> so all surfaces on a page
/// share a single pair of queries.
/// </summary>
public class NavMenuViewComponent(ICatalogService catalog) : ViewComponent
{
    private const string CacheKey = "__ws_nav_menu";

    public async Task<IViewComponentResult> InvokeAsync(string section = "HeaderNav")
    {
        var model = await GetNavAsync();
        return View(section, model);
    }

    private async Task<NavMenuViewModel> GetNavAsync()
    {
        if (HttpContext.Items.TryGetValue(CacheKey, out var cached) && cached is NavMenuViewModel existing)
        {
            return existing;
        }

        var ct = HttpContext.RequestAborted;
        var categories = await catalog.GetCategoryTreeAsync(ct);
        var collections = await catalog.GetCollectionsAsync(ct);

        var model = new NavMenuViewModel { Categories = categories, Collections = collections };
        HttpContext.Items[CacheKey] = model;
        return model;
    }
}
