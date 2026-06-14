using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class BannerService(WhiteStichesDbContext db) : IBannerService
{
    public Task<Banner?> GetActiveHeroAsync(CancellationToken ct = default) =>
        db.Banners.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderByDescending(b => b.SortOrder)
            .ThenByDescending(b => b.Id)
            .Include(b => b.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .Include(b => b.Stats.OrderBy(s => s.SortOrder).ThenBy(s => s.Id))
            .FirstOrDefaultAsync(ct);
}
