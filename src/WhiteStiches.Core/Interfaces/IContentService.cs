using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Models;

namespace WhiteStiches.Core.Interfaces;

/// <summary>Editorial and static content — pages, journal, contact messages (AD-CNT-01/02).</summary>
public interface IContentService
{
    Task<StaticPage?> GetPageBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<StaticPage>> GetPagesAsync(CancellationToken ct = default);
    Task<StaticPage> SavePageAsync(StaticPage page, CancellationToken ct = default);

    Task<PagedResult<JournalPost>> GetPublishedPostsAsync(string? categorySlug = null, int page = 1, int pageSize = 9, CancellationToken ct = default);
    Task<JournalPost?> GetPostBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<JournalPost>> GetRelatedPostsAsync(int postId, int count = 3, CancellationToken ct = default);
    Task<IReadOnlyList<JournalCategory>> GetJournalCategoriesAsync(CancellationToken ct = default);
    Task<JournalPost> SavePostAsync(JournalPost post, CancellationToken ct = default);
    Task DeletePostAsync(int id, CancellationToken ct = default);

    Task<ContactMessage> SubmitContactMessageAsync(ContactMessage message, CancellationToken ct = default);
    Task<PagedResult<ContactMessage>> GetContactMessagesAsync(bool unreadOnly = false, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task MarkContactMessageReadAsync(int id, CancellationToken ct = default);

    // --- Admin back office (AD-CNT-01/02) ---

    Task<StaticPage?> GetPageByIdAsync(int id, CancellationToken ct = default);
    Task DeletePageAsync(int id, CancellationToken ct = default);

    /// <summary>Returns <paramref name="desiredSlug"/> or, on collision, the first free "-2"/"-3"... variant.</summary>
    Task<string> EnsureUniquePageSlugAsync(string desiredSlug, int excludeId = 0, CancellationToken ct = default);

    /// <summary>All journal posts including drafts and scheduled, newest first, with optional title/slug search.</summary>
    Task<PagedResult<JournalPost>> GetPostsAdminAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<JournalPost?> GetPostByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns <paramref name="desiredSlug"/> or, on collision, the first free "-2"/"-3"... variant.</summary>
    Task<string> EnsureUniquePostSlugAsync(string desiredSlug, int excludeId = 0, CancellationToken ct = default);

    /// <summary>Creates or updates a journal category; blank slug is generated from the EN name and made unique.</summary>
    Task<JournalCategory> SaveJournalCategoryAsync(JournalCategory category, CancellationToken ct = default);

    Task<ContactMessage?> GetContactMessageAsync(int id, CancellationToken ct = default);
}
