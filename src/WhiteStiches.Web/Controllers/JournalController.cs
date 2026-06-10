using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Web.Models.Content;

namespace WhiteStiches.Web.Controllers;

public class JournalController(IContentService contentService) : Controller
{
    private const int PageSize = 9;

    [HttpGet("journal")]
    public async Task<IActionResult> Index(string? category = null, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

        var posts = await contentService.GetPublishedPostsAsync(category, page, PageSize, ct);
        var categories = await contentService.GetJournalCategoriesAsync(ct);

        var featured = posts.Page == 1 && posts.Items.Count > 0 ? posts.Items[0] : null;
        var grid = featured is null ? posts.Items : posts.Items.Skip(1).ToList();

        return View(new JournalIndexViewModel
        {
            Featured = featured,
            GridPosts = grid,
            Categories = categories,
            ActiveCategorySlug = category,
            Page = posts.Page,
            TotalPages = posts.TotalPages
        });
    }

    /// <summary>Legacy static-site URL — forwards to the newest published post.</summary>
    [HttpGet("journal/post")]
    public async Task<IActionResult> LegacyPost(CancellationToken ct = default)
    {
        var newest = await contentService.GetPublishedPostsAsync(page: 1, pageSize: 1, ct: ct);
        var post = newest.Items.FirstOrDefault();

        return post is null
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Post), new { slug = post.Slug });
    }

    [HttpGet("journal/{slug}")]
    public async Task<IActionResult> Post(string slug, CancellationToken ct = default)
    {
        var post = await contentService.GetPostBySlugAsync(slug, ct);
        if (post is null) return NotFound();

        var related = await contentService.GetRelatedPostsAsync(post.Id, 3, ct);

        return View(new JournalPostViewModel { Post = post, Related = related });
    }
}
