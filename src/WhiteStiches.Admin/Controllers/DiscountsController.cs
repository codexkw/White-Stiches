using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Discount code management (AD-MKT-01).</summary>
[Route("discounts")]
public class DiscountsController(IMarketingService marketing, IAuditService audit) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, bool activeOnly = false, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Discounts";
        ViewData["Nav"] = "discounts";

        var discounts = await marketing.GetDiscountCodesPagedAsync(search, activeOnly, page, 20, ct);

        return View(new DiscountListViewModel
        {
            Discounts = discounts,
            Search = search,
            ActiveOnly = activeOnly
        });
    }

    [HttpGet("new")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New discount";
        ViewData["Nav"] = "discounts";
        return View("Edit", new DiscountEditViewModel());
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, string? eligSearch, CancellationToken ct)
    {
        var entity = await marketing.GetDiscountCodeAsync(id, ct);
        if (entity is null)
        {
            TempData["Err"] = "Discount code not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit {entity.Code}";
        ViewData["Nav"] = "discounts";

        var model = DiscountEditViewModel.From(entity);
        await PopulateEligibilityAsync(model, eligSearch, ct);
        return View("Edit", model);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DiscountEditViewModel model, CancellationToken ct)
    {
        model.Code = (model.Code ?? string.Empty).Trim().ToUpperInvariant();

        if (model.Code.Length == 0)
        {
            if (!ModelState.TryGetValue(nameof(model.Code), out var codeState) || codeState.Errors.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Code), "Code is required.");
            }
        }
        else if (model.Code.Any(char.IsWhiteSpace))
        {
            ModelState.AddModelError(nameof(model.Code), "Code cannot contain spaces.");
        }
        else if (await marketing.DiscountCodeExistsAsync(model.Code, model.Id, ct))
        {
            ModelState.AddModelError(nameof(model.Code), $"Code \"{model.Code}\" already exists.");
        }

        if (model.Type == DiscountType.FreeShipping)
        {
            // Value is ignored for free shipping — clear any binding noise and store 0.
            ModelState.Remove(nameof(model.Value));
            model.Value = 0m;
        }
        else if (model.Value <= 0)
        {
            ModelState.AddModelError(nameof(model.Value), "Value must be greater than zero.");
        }
        else if (model.Type == DiscountType.Percentage && model.Value > 100)
        {
            ModelState.AddModelError(nameof(model.Value), "Percentage cannot exceed 100.");
        }

        if (model.MinPurchaseAmount is decimal minPurchase && minPurchase < 0m)
        {
            ModelState.AddModelError(nameof(model.MinPurchaseAmount), "Minimum purchase cannot be negative.");
        }

        if (model.StartsAtUtc is not null && model.EndsAtUtc is not null && model.EndsAtUtc <= model.StartsAtUtc)
        {
            ModelState.AddModelError(nameof(model.EndsAtUtc), "End of schedule must be after the start.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = model.Id == 0 ? "New discount" : "Edit discount";
            ViewData["Nav"] = "discounts";
            await PopulateEligibilityAsync(model, null, ct);
            return View("Edit", model);
        }

        var (userId, userName) = CurrentUser();

        if (model.Id == 0)
        {
            var entity = new DiscountCode();
            model.Apply(entity);
            await marketing.SaveDiscountCodeAsync(entity, ct);

            await audit.LogAsync("discount.create", userId, userName,
                "DiscountCode", entity.Id.ToString(), after: Snapshot(entity), ct: ct);

            TempData["Ok"] = $"Discount code {entity.Code} created.";
        }
        else
        {
            var entity = await marketing.GetDiscountCodeAsync(model.Id, ct);
            if (entity is null)
            {
                TempData["Err"] = "Discount code not found.";
                return RedirectToAction(nameof(Index));
            }

            var before = Snapshot(entity);
            model.Apply(entity);
            await marketing.SaveDiscountCodeAsync(entity, ct);

            await audit.LogAsync("discount.update", userId, userName,
                "DiscountCode", entity.Id.ToString(), before, Snapshot(entity), ct: ct);

            TempData["Ok"] = $"Discount code {entity.Code} updated.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await marketing.GetDiscountCodeAsync(id, ct);
        if (entity is null)
        {
            TempData["Err"] = "Discount code not found.";
            return RedirectToAction(nameof(Index));
        }

        var (userId, userName) = CurrentUser();
        var before = Snapshot(entity);

        if (await marketing.IsDiscountCodeUsedByOrdersAsync(id, ct))
        {
            entity.IsActive = false;
            await marketing.SaveDiscountCodeAsync(entity, ct);

            await audit.LogAsync("discount.deactivate", userId, userName,
                "DiscountCode", id.ToString(), before, Snapshot(entity), ct: ct);

            TempData["Ok"] = $"Code {entity.Code} is referenced by existing orders, so it was deactivated instead of deleted.";
        }
        else
        {
            await marketing.DeleteDiscountCodeAsync(id, ct);

            await audit.LogAsync("discount.delete", userId, userName,
                "DiscountCode", id.ToString(), before, ct: ct);

            TempData["Ok"] = $"Discount code {entity.Code} deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- eligibility picker

    [HttpPost("{id:int}/eligibility/products/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEligibleProduct(int id, int productId, string? eligSearch, CancellationToken ct)
    {
        await marketing.AddEligibleProductAsync(id, productId, ct);
        return RedirectToAction(nameof(Edit), new { id, eligSearch });
    }

    [HttpPost("{id:int}/eligibility/products/{productId:int}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveEligibleProduct(int id, int productId, CancellationToken ct)
    {
        await marketing.RemoveEligibleProductAsync(id, productId, ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/eligibility/collections")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEligibleCollections(int id, int[]? collectionIds, CancellationToken ct)
    {
        await marketing.SetEligibleCollectionsAsync(id, collectionIds ?? [], ct);
        TempData["Ok"] = "Eligible collections updated.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Loads the eligibility picker state (selected products, all collections, optional search) for an existing code.</summary>
    private async Task PopulateEligibilityAsync(DiscountEditViewModel model, string? search, CancellationToken ct)
    {
        if (model.Id == 0) return; // eligibility is curated after the code is first saved

        var eligibility = await marketing.GetEligibilityAsync(model.Id, ct);

        var selected = await marketing.GetProductsByIdsAsync(eligibility.Products, ct);
        model.EligibleProducts = selected.Select(ToRow).ToList();

        var selectedCollectionIds = eligibility.Collections.ToHashSet();
        var collections = await marketing.GetAllCollectionsAsync(ct);
        model.EligibleCollections = collections
            .Select(c => new EligibilityCollectionRow(c.Id, c.TitleEn, selectedCollectionIds.Contains(c.Id)))
            .ToList();

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            model.ProductSearch = term;
            model.ShowSearchResults = true;
            var alreadyIn = eligibility.Products.ToHashSet();
            var results = await marketing.SearchProductsAsync(term, 20, ct);
            model.SearchResults = results.Where(p => !alreadyIn.Contains(p.Id)).Select(ToRow).ToList();
        }
    }

    private static EligibilityProductRow ToRow(Product p) => new(
        p.Id,
        p.TitleEn,
        p.Slug,
        p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
        p.Variants.Count > 0 ? p.Variants.Min(v => v.Price) : null);

    private (Guid? UserId, string? UserName) CurrentUser()
    {
        Guid? userId = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : null;
        return (userId, User.Identity?.Name);
    }

    private static object Snapshot(DiscountCode d) => new
    {
        d.Code,
        Type = d.Type.ToString(),
        d.Value,
        d.MinPurchaseAmount,
        d.MinQuantity,
        d.UsageLimitTotal,
        d.UsageLimitPerCustomer,
        d.TimesUsed,
        d.StartsAtUtc,
        d.EndsAtUtc,
        d.IsActive,
        d.EligibilityJson
    };
}
