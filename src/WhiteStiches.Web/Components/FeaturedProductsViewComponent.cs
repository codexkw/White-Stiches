using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Web.Components;

/// <summary>Renders the featured-products grid (home "Just landed." section).</summary>
public class FeaturedProductsViewComponent(ICatalogService catalog) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int count = 4)
    {
        var products = await catalog.GetFeaturedProductsAsync(count, HttpContext.RequestAborted);
        return View(products);
    }
}
