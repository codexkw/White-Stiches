using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Utils;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Hierarchical category taxonomy back office (AD-PRD-08).</summary>
[Route("categories")]
public class CategoriesController(ICatalogService catalog, IAuditService audit) : Controller
{
    private Guid? UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? UserName => User.Identity?.Name;

    /// <summary>Tree list + inline create form. ?edit={id} pre-fills the form for that category (no-JS editing).</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int? edit, CancellationToken ct)
    {
        ViewData["Title"] = "Categories";
        ViewData["Nav"] = "categories";

        var all = await catalog.GetCategoriesAdminAsync(ct);
        var counts = await catalog.GetCategoryProductCountsAsync(ct);

        var form = new CategoryFormModel();
        if (edit is int editId && all.FirstOrDefault(c => c.Id == editId) is Category editing)
        {
            form = new CategoryFormModel
            {
                Id = editing.Id,
                NameEn = editing.NameEn,
                NameAr = editing.NameAr,
                Slug = editing.Slug,
                ParentId = editing.ParentId,
                SortOrder = editing.SortOrder,
                IsActive = editing.IsActive
            };
        }

        // A category cannot become a child of itself or of its own subtree.
        var excluded = form.Id > 0 ? CategoryTree.SubtreeIds(all, form.Id) : [];
        var parentChoices = CategoryTree.BuildChoices(all.Where(c => !excluded.Contains(c.Id)).ToList());

        return View(new CategoryListViewModel
        {
            Rows = CategoryTree.BuildRows(all, counts),
            Form = form,
            ParentChoices = parentChoices
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CategoryFormModel form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.NameEn))
        {
            TempData["Err"] = "Name (EN) is required.";
            return RedirectToAction(nameof(Index));
        }

        var all = await catalog.GetCategoriesAdminAsync(ct);

        if (form.ParentId is int parentId)
        {
            if (all.All(c => c.Id != parentId))
            {
                TempData["Err"] = $"Parent category {parentId} was not found.";
                return RedirectToAction(nameof(Index));
            }

            if (form.Id > 0 && CategoryTree.SubtreeIds(all, form.Id).Contains(parentId))
            {
                TempData["Err"] = "A category cannot be nested under itself or its own subtree.";
                return RedirectToAction(nameof(Index), new { edit = form.Id });
            }
        }

        var baseSlug = Slug.Generate(string.IsNullOrWhiteSpace(form.Slug) ? form.NameEn : form.Slug);
        if (baseSlug.Length == 0) baseSlug = "category";

        if (form.Id == 0)
        {
            var category = new Category
            {
                NameEn = form.NameEn.Trim(),
                NameAr = form.NameAr?.Trim() ?? string.Empty,
                Slug = await catalog.EnsureUniqueCategorySlugAsync(baseSlug, null, ct),
                ParentId = form.ParentId,
                SortOrder = form.SortOrder,
                IsActive = form.IsActive
            };

            await catalog.CreateCategoryAsync(category, ct);
            await audit.LogAsync("category.create", UserId, UserName, nameof(Category), category.Id.ToString(),
                after: Snapshot(category));

            TempData["Ok"] = $"Category “{category.NameEn}” created.";
        }
        else
        {
            var category = all.FirstOrDefault(c => c.Id == form.Id);
            if (category is null)
            {
                TempData["Err"] = $"Category {form.Id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            var before = Snapshot(category);
            category.NameEn = form.NameEn.Trim();
            category.NameAr = form.NameAr?.Trim() ?? string.Empty;
            category.Slug = await catalog.EnsureUniqueCategorySlugAsync(baseSlug, category.Id, ct);
            category.ParentId = form.ParentId;
            category.SortOrder = form.SortOrder;
            category.IsActive = form.IsActive;

            await catalog.UpdateCategoryAsync(category, ct);
            await audit.LogAsync("category.update", UserId, UserName, nameof(Category), category.Id.ToString(),
                before: before, after: Snapshot(category));

            TempData["Ok"] = $"Category “{category.NameEn}” saved.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var all = await catalog.GetCategoriesAdminAsync(ct);
        var category = all.FirstOrDefault(c => c.Id == id);
        if (category is null)
        {
            TempData["Err"] = $"Category {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (all.Any(c => c.ParentId == id))
        {
            TempData["Err"] = $"“{category.NameEn}” has child categories — move or delete them first.";
            return RedirectToAction(nameof(Index));
        }

        var counts = await catalog.GetCategoryProductCountsAsync(ct);
        if (counts.TryGetValue(id, out var productCount) && productCount > 0)
        {
            TempData["Err"] = $"“{category.NameEn}” still has {productCount} product(s) — reassign them first.";
            return RedirectToAction(nameof(Index));
        }

        await catalog.DeleteCategoryAsync(id, ct);
        await audit.LogAsync("category.delete", UserId, UserName, nameof(Category), id.ToString(),
            before: Snapshot(category));

        TempData["Ok"] = $"Category “{category.NameEn}” deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static object Snapshot(Category c) => new
    {
        c.Id,
        c.NameEn,
        c.NameAr,
        c.Slug,
        c.ParentId,
        c.SortOrder,
        c.IsActive
    };
}
