using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models;
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
}
