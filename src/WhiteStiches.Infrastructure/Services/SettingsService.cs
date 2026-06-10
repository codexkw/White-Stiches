using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WhiteStiches.Core.Entities.Settings;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Infrastructure.Data;

namespace WhiteStiches.Infrastructure.Services;

public class SettingsService(WhiteStichesDbContext db, IMemoryCache cache) : ISettingsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync($"setting:{key}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await db.StoreSettings
                .AsNoTracking()
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);
        });
    }

    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, ct);
        if (raw is null) return defaultValue;

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T)Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return defaultValue;
        }
    }

    public async Task SetAsync(string key, string? value, string? group = null, CancellationToken ct = default)
    {
        var setting = await db.StoreSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting is null)
        {
            db.StoreSettings.Add(new StoreSetting { Key = key, Value = value, Group = group });
        }
        else
        {
            setting.Value = value;
            if (group is not null) setting.Group = group;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove($"setting:{key}");
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetGroupAsync(string group, CancellationToken ct = default) =>
        await db.StoreSettings
            .AsNoTracking()
            .Where(s => s.Group == group)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
}
