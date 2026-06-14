using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services.Admin;

public class BannerAdminService(WhiteStichesDbContext db) : IBannerAdminService
{
    public async Task<PagedResult<BannerListItem>> GetListAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = db.Banners.AsNoTracking()
            .OrderByDescending(b => b.IsActive)
            .ThenByDescending(b => b.SortOrder)
            .ThenByDescending(b => b.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BannerListItem(
                b.Id,
                b.AdminLabel,
                b.TitleLine1En,
                b.IsActive,
                b.SortOrder,
                b.Images.Count,
                b.Stats.Count,
                b.UpdatedAtUtc ?? b.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<BannerListItem> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<Banner?> GetForEditAsync(int id, CancellationToken ct = default) =>
        db.Banners.AsNoTracking()
            .Include(b => b.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .Include(b => b.Stats.OrderBy(s => s.SortOrder).ThenBy(s => s.Id))
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<Banner> SaveAsync(Banner banner, IReadOnlyList<BannerStat> stats, CancellationToken ct = default)
    {
        var ordered = stats
            .Select((s, i) => new BannerStat
            {
                Value = s.Value,
                LabelEn = s.LabelEn,
                LabelAr = s.LabelAr,
                IsVisible = s.IsVisible,
                SortOrder = i
            })
            .ToList();

        if (banner.Id == 0)
        {
            banner.Stats = ordered;
            db.Banners.Add(banner);
            await db.SaveChangesAsync(ct);
            return banner;
        }

        var existing = await db.Banners
            .Include(b => b.Stats)
            .FirstOrDefaultAsync(b => b.Id == banner.Id, ct)
            ?? throw new InvalidOperationException($"Banner {banner.Id} not found.");

        existing.AdminLabel = banner.AdminLabel;
        existing.EyebrowEn = banner.EyebrowEn;
        existing.EyebrowAr = banner.EyebrowAr;
        existing.TitleLine1En = banner.TitleLine1En;
        existing.TitleLine1Ar = banner.TitleLine1Ar;
        existing.TitleLine2En = banner.TitleLine2En;
        existing.TitleLine2Ar = banner.TitleLine2Ar;
        existing.TitleLine2Italic = banner.TitleLine2Italic;
        existing.LedeEn = banner.LedeEn;
        existing.LedeAr = banner.LedeAr;
        existing.PrimaryCtaTextEn = banner.PrimaryCtaTextEn;
        existing.PrimaryCtaTextAr = banner.PrimaryCtaTextAr;
        existing.PrimaryCtaUrl = banner.PrimaryCtaUrl;
        existing.SecondaryCtaTextEn = banner.SecondaryCtaTextEn;
        existing.SecondaryCtaTextAr = banner.SecondaryCtaTextAr;
        existing.SecondaryCtaUrl = banner.SecondaryCtaUrl;
        existing.IsActive = banner.IsActive;
        existing.ShowStats = banner.ShowStats;
        existing.SortOrder = banner.SortOrder;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        // Replace the stat set wholesale (drop old rows, insert the submitted ones).
        db.BannerStats.RemoveRange(existing.Stats);
        foreach (var s in ordered)
        {
            s.BannerId = existing.Id;
            db.BannerStats.Add(s);
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return false;

        // Images and stats cascade-delete via the FK configuration.
        db.Banners.Remove(banner);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<BannerImage?> AddImageAsync(int bannerId, string url, MediaKind kind, CancellationToken ct = default)
    {
        if (!await db.Banners.AnyAsync(b => b.Id == bannerId, ct)) return null;

        var maxSort = await db.BannerImages
            .Where(i => i.BannerId == bannerId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct) ?? -1;

        var image = new BannerImage { BannerId = bannerId, Url = url, MediaKind = kind, SortOrder = maxSort + 1 };
        db.BannerImages.Add(image);
        await TouchBannerAsync(bannerId, ct);
        await db.SaveChangesAsync(ct);
        return image;
    }

    public async Task<string?> DeleteImageAsync(int bannerId, int imageId, CancellationToken ct = default)
    {
        var image = await db.BannerImages.FirstOrDefaultAsync(i => i.Id == imageId && i.BannerId == bannerId, ct);
        if (image is null) return null;

        var url = image.Url;
        db.BannerImages.Remove(image);
        await TouchBannerAsync(bannerId, ct);
        await db.SaveChangesAsync(ct);
        return url;
    }

    public async Task<bool> MoveImageAsync(int bannerId, int imageId, bool moveUp, CancellationToken ct = default)
    {
        var images = await db.BannerImages
            .Where(i => i.BannerId == bannerId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .ToListAsync(ct);

        var idx = images.FindIndex(i => i.Id == imageId);
        if (idx < 0) return false;

        var target = moveUp ? idx - 1 : idx + 1;
        if (target < 0 || target >= images.Count) return false;

        (images[idx], images[target]) = (images[target], images[idx]);
        for (var i = 0; i < images.Count; i++) images[i].SortOrder = i;

        await TouchBannerAsync(bannerId, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool?> ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return null;

        banner.IsActive = !banner.IsActive;
        banner.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return banner.IsActive;
    }

    private async Task TouchBannerAsync(int bannerId, CancellationToken ct)
    {
        var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == bannerId, ct);
        if (banner is not null) banner.UpdatedAtUtc = DateTime.UtcNow;
    }
}
