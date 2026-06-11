using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Static page editor (AD-CNT-01). Routes under /pages.</summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.MarketingManager},{AppRoles.ContentEditor}")]
public class PagesAdminController(IContentService content, IAuditService audit, IRichTextSanitizer sanitizer) : Controller
{
    private const int PageSize = 25;

    [HttpGet("pages")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Pages";
        ViewData["Nav"] = "pages";

        var pages = await content.GetPagesAsync(ct);
        return View(pages.ToPagedResult(page, PageSize));
    }

    [HttpGet("pages/new")]
    public IActionResult New()
    {
        ViewData["Title"] = "New page";
        ViewData["Nav"] = "pages";

        return View("Edit", new PageEditViewModel());
    }

    [HttpGet("pages/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var page = await content.GetPageByIdAsync(id, ct);
        if (page is null)
        {
            TempData["Err"] = "Page not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit page — {page.TitleEn}";
        ViewData["Nav"] = "pages";

        return View("Edit", new PageEditViewModel
        {
            Id = page.Id,
            TitleEn = page.TitleEn,
            TitleAr = page.TitleAr,
            Slug = page.Slug,
            BodyEn = page.BodyEn,
            BodyAr = page.BodyAr,
            SeoTitleEn = page.SeoTitleEn,
            SeoTitleAr = page.SeoTitleAr,
            SeoDescriptionEn = page.SeoDescriptionEn,
            SeoDescriptionAr = page.SeoDescriptionAr,
            IsPublished = page.IsPublished
        });
    }

    [HttpPost("pages/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PageEditViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = model.Id == 0 ? "New page" : $"Edit page — {model.TitleEn}";
            ViewData["Nav"] = "pages";
            return View("Edit", model);
        }

        var slug = Core.Utils.Slug.Generate(string.IsNullOrWhiteSpace(model.Slug) ? model.TitleEn : model.Slug);
        if (string.IsNullOrEmpty(slug)) slug = "page";
        slug = await content.EnsureUniquePageSlugAsync(slug, model.Id, ct);

        StaticPage page;
        object? before = null;

        if (model.Id > 0)
        {
            var existing = await content.GetPageByIdAsync(model.Id, ct);
            if (existing is null)
            {
                TempData["Err"] = "Page not found.";
                return RedirectToAction(nameof(Index));
            }

            before = Snapshot(existing);
            page = existing;
        }
        else
        {
            page = new StaticPage();
        }

        page.Slug = slug;
        page.TitleEn = model.TitleEn.Trim();
        page.TitleAr = model.TitleAr?.Trim() ?? string.Empty;
        page.BodyEn = sanitizer.Sanitize(model.BodyEn);
        page.BodyAr = sanitizer.Sanitize(model.BodyAr);
        page.SeoTitleEn = model.SeoTitleEn;
        page.SeoTitleAr = model.SeoTitleAr;
        page.SeoDescriptionEn = model.SeoDescriptionEn;
        page.SeoDescriptionAr = model.SeoDescriptionAr;
        page.IsPublished = model.IsPublished;

        await content.SavePageAsync(page, ct);

        await audit.LogAsync(model.Id > 0 ? "content.page.update" : "content.page.create",
            CurrentUserId(), User.Identity?.Name,
            nameof(StaticPage), page.Id.ToString(),
            before, Snapshot(page), ct: ct);

        TempData["Ok"] = model.Id > 0 ? $"Page “{page.TitleEn}” updated." : $"Page “{page.TitleEn}” created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("pages/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? confirm, CancellationToken ct)
    {
        if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Err"] = "Deletion not confirmed — tick the confirmation box first.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var page = await content.GetPageByIdAsync(id, ct);
        if (page is null)
        {
            TempData["Err"] = "Page not found.";
            return RedirectToAction(nameof(Index));
        }

        await content.DeletePageAsync(id, ct);

        await audit.LogAsync("content.page.delete",
            CurrentUserId(), User.Identity?.Name,
            nameof(StaticPage), id.ToString(),
            Snapshot(page), null, ct: ct);

        TempData["Ok"] = $"Page “{page.TitleEn}” deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static object Snapshot(StaticPage p) => new
    {
        p.Id,
        p.Slug,
        p.TitleEn,
        p.TitleAr,
        p.BodyEn,
        p.BodyAr,
        p.SeoTitleEn,
        p.SeoTitleAr,
        p.SeoDescriptionEn,
        p.SeoDescriptionAr,
        p.IsPublished
    };
}
