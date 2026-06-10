namespace WhiteStiches.Core.Interfaces;

/// <summary>Store settings key-value access (AD-SET-01).</summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken ct = default);
    Task SetAsync(string key, string? value, string? group = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string?>> GetGroupAsync(string group, CancellationToken ct = default);
}

/// <summary>Well-known setting keys.</summary>
public static class SettingKeys
{
    public const string StoreNameEn = "store.name.en";
    public const string StoreNameAr = "store.name.ar";
    public const string ContactEmail = "store.contact.email";
    public const string ContactPhone = "store.contact.phone";
    public const string WhatsAppNumber = "store.whatsapp.number";
    public const string FreeShippingThreshold = "shipping.free_threshold";
    public const string StandardShippingRate = "shipping.standard_rate";
    public const string ExpressShippingRate = "shipping.express_rate";
    public const string SameDayShippingRate = "shipping.same_day_rate";
    public const string GiftWrapFee = "cart.gift_wrap_fee";
    public const string MaintenanceMode = "store.maintenance_mode";
    public const string AnnouncementMessages = "store.announcement.messages";
    public const string InstagramUrl = "store.social.instagram";
    public const string TikTokUrl = "store.social.tiktok";
    public const string PinterestUrl = "store.social.pinterest";
}
