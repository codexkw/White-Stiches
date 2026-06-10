using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Utils;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class ContentService(WhiteStichesDbContext db) : IContentService
{
    public Task<StaticPage?> GetPageBySlugAsync(string slug, CancellationToken ct = default) =>
        db.StaticPages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);

    public async Task<IReadOnlyList<StaticPage>> GetPagesAsync(CancellationToken ct = default) =>
        await db.StaticPages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync(ct);

    public async Task<StaticPage> SavePageAsync(StaticPage page, CancellationToken ct = default)
    {
        if (page.Id == 0)
        {
            db.StaticPages.Add(page);
        }
        else
        {
            db.StaticPages.Update(page);
        }

        await db.SaveChangesAsync(ct);
        return page;
    }

    public async Task<PagedResult<JournalPost>> GetPublishedPostsAsync(string? categorySlug = null,
        int page = 1, int pageSize = 9, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = db.JournalPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsPublished && (p.PublishAtUtc == null || p.PublishAtUtc <= now));

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            query = query.Where(p => p.Category != null && p.Category.Slug == categorySlug);
        }

        query = query.OrderByDescending(p => p.PublishAtUtc ?? p.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<JournalPost> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<JournalPost?> GetPostBySlugAsync(string slug, CancellationToken ct = default) =>
        db.JournalPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);

    public async Task<IReadOnlyList<JournalPost>> GetRelatedPostsAsync(int postId, int count = 3, CancellationToken ct = default)
    {
        var categoryId = await db.JournalPosts
            .Where(p => p.Id == postId)
            .Select(p => p.JournalCategoryId)
            .FirstOrDefaultAsync(ct);

        return await db.JournalPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Id != postId && p.IsPublished)
            .OrderByDescending(p => p.JournalCategoryId == categoryId)
            .ThenByDescending(p => p.PublishAtUtc ?? p.CreatedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<JournalCategory>> GetJournalCategoriesAsync(CancellationToken ct = default) =>
        await db.JournalCategories.AsNoTracking().OrderBy(c => c.NameEn).ToListAsync(ct);

    public async Task<JournalPost> SavePostAsync(JournalPost post, CancellationToken ct = default)
    {
        if (post.Id == 0)
        {
            db.JournalPosts.Add(post);
        }
        else
        {
            db.JournalPosts.Update(post);
        }

        await db.SaveChangesAsync(ct);
        return post;
    }

    public async Task DeletePostAsync(int id, CancellationToken ct = default)
    {
        var post = await db.JournalPosts.FindAsync([id], ct);
        if (post is null) return;

        db.JournalPosts.Remove(post);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ContactMessage> SubmitContactMessageAsync(ContactMessage message, CancellationToken ct = default)
    {
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<PagedResult<ContactMessage>> GetContactMessagesAsync(bool unreadOnly = false,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = db.ContactMessages.AsNoTracking().AsQueryable();

        if (unreadOnly)
        {
            query = query.Where(m => !m.IsRead);
        }

        query = query.OrderByDescending(m => m.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<ContactMessage> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task MarkContactMessageReadAsync(int id, CancellationToken ct = default)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null) return;

        message.IsRead = true;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------
    // Admin back office (AD-CNT-01/02)
    // ------------------------------------------------------------------

    public Task<StaticPage?> GetPageByIdAsync(int id, CancellationToken ct = default) =>
        db.StaticPages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task DeletePageAsync(int id, CancellationToken ct = default)
    {
        var page = await db.StaticPages.FindAsync([id], ct);
        if (page is null) return;

        db.StaticPages.Remove(page);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> EnsureUniquePageSlugAsync(string desiredSlug, int excludeId = 0, CancellationToken ct = default)
    {
        var slug = desiredSlug;
        var suffix = 2;
        while (await db.StaticPages.AsNoTracking().AnyAsync(p => p.Slug == slug && p.Id != excludeId, ct))
        {
            slug = $"{desiredSlug}-{suffix++}";
        }

        return slug;
    }

    public async Task<PagedResult<JournalPost>> GetPostsAdminAsync(string? search = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.JournalPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.TitleEn.Contains(term) || p.TitleAr.Contains(term) || p.Slug.Contains(term));
        }

        query = query.OrderByDescending(p => p.PublishAtUtc ?? p.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<JournalPost> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<JournalPost?> GetPostByIdAsync(int id, CancellationToken ct = default) =>
        db.JournalPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<string> EnsureUniquePostSlugAsync(string desiredSlug, int excludeId = 0, CancellationToken ct = default)
    {
        var slug = desiredSlug;
        var suffix = 2;
        while (await db.JournalPosts.AsNoTracking().AnyAsync(p => p.Slug == slug && p.Id != excludeId, ct))
        {
            slug = $"{desiredSlug}-{suffix++}";
        }

        return slug;
    }

    public async Task<JournalCategory> SaveJournalCategoryAsync(JournalCategory category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(category.Slug))
        {
            category.Slug = Slug.Generate(category.NameEn);
        }

        if (string.IsNullOrWhiteSpace(category.Slug))
        {
            category.Slug = "category";
        }

        var baseSlug = category.Slug;
        var suffix = 2;
        while (await db.JournalCategories.AsNoTracking().AnyAsync(c => c.Slug == category.Slug && c.Id != category.Id, ct))
        {
            category.Slug = $"{baseSlug}-{suffix++}";
        }

        if (category.Id == 0)
        {
            db.JournalCategories.Add(category);
        }
        else
        {
            db.JournalCategories.Update(category);
        }

        await db.SaveChangesAsync(ct);
        return category;
    }

    public Task<ContactMessage?> GetContactMessageAsync(int id, CancellationToken ct = default) =>
        db.ContactMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
}
