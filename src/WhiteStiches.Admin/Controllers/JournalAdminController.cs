using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Journal post management incl. drafts, categories, hero images (AD-CNT-02). Routes under /journal.</summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.MarketingManager},{AppRoles.ContentEditor}")]
public class JournalAdminController(IContentService content, IFileStorage files, IAuditService audit) : Controller
{
    [HttpGet("journal")]
    public async Task<IActionResult> Index(string? search, int page = 1, int size = 20, CancellationToken ct = default)
    {
        ViewData["Title"] = "Journal";
        ViewData["Nav"] = "journal";

        var posts = await content.GetPostsAdminAsync(search, page, size, ct);
        return View(new JournalListViewModel { Posts = posts, Search = search });
    }

    [HttpGet("journal/new")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        ViewData["Title"] = "New post";
        ViewData["Nav"] = "journal";

        return View("Edit", new JournalEditViewModel
        {
            AuthorName = User.Identity?.Name,
            Categories = await content.GetJournalCategoriesAsync(ct)
        });
    }

    [HttpGet("journal/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var post = await content.GetPostByIdAsync(id, ct);
        if (post is null)
        {
            TempData["Err"] = "Post not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Edit post — {post.TitleEn}";
        ViewData["Nav"] = "journal";

        return View("Edit", new JournalEditViewModel
        {
            Id = post.Id,
            TitleEn = post.TitleEn,
            TitleAr = post.TitleAr,
            Slug = post.Slug,
            ExcerptEn = post.ExcerptEn,
            ExcerptAr = post.ExcerptAr,
            BodyEn = post.BodyEn,
            BodyAr = post.BodyAr,
            JournalCategoryId = post.JournalCategoryId,
            Tags = post.Tags,
            AuthorName = post.AuthorName,
            HeroImageUrl = post.HeroImageUrl,
            IsPublished = post.IsPublished,
            PublishAtUtc = post.PublishAtUtc?.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            ReadingTimeMinutes = post.ReadingTimeMinutes,
            Categories = await content.GetJournalCategoriesAsync(ct)
        });
    }

    [HttpPost("journal/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(JournalEditViewModel model, CancellationToken ct)
    {
        DateTime? publishAt = null;
        if (!string.IsNullOrWhiteSpace(model.PublishAtUtc))
        {
            if (DateTime.TryParse(model.PublishAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                publishAt = parsed;
            }
            else
            {
                ModelState.AddModelError(nameof(model.PublishAtUtc), "Publish date must look like 2026-06-10 14:30 (UTC).");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = model.Id == 0 ? "New post" : $"Edit post — {model.TitleEn}";
            ViewData["Nav"] = "journal";
            model.Categories = await content.GetJournalCategoriesAsync(ct);
            return View("Edit", model);
        }

        var categoryId = model.JournalCategoryId;

        // Inline quick-create: a filled NewCategoryName wins over the select.
        if (!string.IsNullOrWhiteSpace(model.NewCategoryName))
        {
            var name = model.NewCategoryName.Trim();
            var category = await content.SaveJournalCategoryAsync(new JournalCategory
            {
                NameEn = name,
                NameAr = name
            }, ct);
            categoryId = category.Id;

            await audit.LogAsync("content.journal.category.create",
                CurrentUserId(), User.Identity?.Name,
                nameof(JournalCategory), category.Id.ToString(),
                null, new { category.Id, category.NameEn, category.NameAr, category.Slug }, ct: ct);
        }

        var slug = Core.Utils.Slug.Generate(string.IsNullOrWhiteSpace(model.Slug) ? model.TitleEn : model.Slug);
        if (string.IsNullOrEmpty(slug)) slug = "post";
        slug = await content.EnsureUniquePostSlugAsync(slug, model.Id, ct);

        JournalPost post;
        object? before = null;

        if (model.Id > 0)
        {
            var existing = await content.GetPostByIdAsync(model.Id, ct);
            if (existing is null)
            {
                TempData["Err"] = "Post not found.";
                return RedirectToAction(nameof(Index));
            }

            before = Snapshot(existing);
            existing.Category = null; // avoid re-attaching the included navigation on Update
            post = existing;
        }
        else
        {
            post = new JournalPost();
        }

        var heroUrl = string.IsNullOrWhiteSpace(model.HeroImageUrl) ? post.HeroImageUrl : model.HeroImageUrl;
        if (model.HeroImage is { Length: > 0 })
        {
            heroUrl = await files.SaveAsync(model.HeroImage.OpenReadStream(), model.HeroImage.FileName, "journal", ct);
        }

        post.Slug = slug;
        post.TitleEn = model.TitleEn.Trim();
        post.TitleAr = model.TitleAr?.Trim() ?? string.Empty;
        post.ExcerptEn = model.ExcerptEn;
        post.ExcerptAr = model.ExcerptAr;
        post.BodyEn = model.BodyEn;
        post.BodyAr = model.BodyAr;
        post.JournalCategoryId = categoryId;
        post.Tags = model.Tags;
        post.AuthorName = string.IsNullOrWhiteSpace(model.AuthorName)
            ? User.Identity?.Name ?? "White Stitches"
            : model.AuthorName.Trim();
        post.HeroImageUrl = heroUrl;
        post.IsPublished = model.IsPublished;
        post.PublishAtUtc = publishAt;
        post.ReadingTimeMinutes = model.ReadingTimeMinutes;

        await content.SavePostAsync(post, ct);

        await audit.LogAsync(model.Id > 0 ? "content.journal.post.update" : "content.journal.post.create",
            CurrentUserId(), User.Identity?.Name,
            nameof(JournalPost), post.Id.ToString(),
            before, Snapshot(post), ct: ct);

        TempData["Ok"] = model.Id > 0 ? $"Post “{post.TitleEn}” updated." : $"Post “{post.TitleEn}” created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("journal/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? confirm, CancellationToken ct)
    {
        if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Err"] = "Deletion not confirmed — tick the confirmation box first.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var post = await content.GetPostByIdAsync(id, ct);
        if (post is null)
        {
            TempData["Err"] = "Post not found.";
            return RedirectToAction(nameof(Index));
        }

        await content.DeletePostAsync(id, ct);

        await audit.LogAsync("content.journal.post.delete",
            CurrentUserId(), User.Identity?.Name,
            nameof(JournalPost), id.ToString(),
            Snapshot(post), null, ct: ct);

        TempData["Ok"] = $"Post “{post.TitleEn}” deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static object Snapshot(JournalPost p) => new
    {
        p.Id,
        p.Slug,
        p.TitleEn,
        p.TitleAr,
        p.ExcerptEn,
        p.ExcerptAr,
        p.BodyEn,
        p.BodyAr,
        p.JournalCategoryId,
        p.Tags,
        p.AuthorName,
        p.HeroImageUrl,
        p.IsPublished,
        p.PublishAtUtc,
        p.ReadingTimeMinutes
    };
}
