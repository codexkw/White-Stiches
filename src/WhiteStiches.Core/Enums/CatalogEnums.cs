namespace WhiteStiches.Core.Enums;

public enum ProductStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}

public enum CollectionSortOrder
{
    Manual = 0,
    BestSelling = 1,
    Alphabetical = 2,
    PriceLowToHigh = 3,
    PriceHighToLow = 4,
    Newest = 5
}

public enum InventoryAdjustmentReason
{
    Received = 0,
    Correction = 1,
    Damage = 2,
    Theft = 3,
    Restock = 4,
    Sale = 5,
    ReturnRestock = 6
}

/// <summary>What a <c>ProductImage</c> row actually holds — a still photo or a playable clip.</summary>
public enum MediaKind
{
    Image = 0,
    Video = 1
}

/// <summary>
/// Classifies product media by file extension. <see cref="VideoExtensions"/> is the single source of
/// truth shared by the uploader (which formats are accepted) and the storefront/admin render decision
/// (img vs. video), so the two layers can never drift apart.
/// </summary>
public static class MediaKinds
{
    /// <summary>Browser-playable video extensions — H.264 MP4 + WebM cover every modern browser.</summary>
    public static readonly IReadOnlySet<string> VideoExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };

    private static readonly char[] QueryDelimiters = ['?', '#'];

    /// <summary>True when the file name / URL ends in an accepted video extension.</summary>
    public static bool IsVideo(string? fileNameOrUrl)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrUrl)) return false;
        var clean = fileNameOrUrl.Trim();
        // Drop any query string / fragment so a URL like "clip.mp4?v=2#t" still resolves to ".mp4".
        var cut = clean.IndexOfAny(QueryDelimiters);
        if (cut >= 0) clean = clean[..cut];
        var ext = Path.GetExtension(clean);
        return ext.Length > 0 && VideoExtensions.Contains(ext);
    }

    /// <summary>Classifies a file name / URL as <see cref="MediaKind.Video"/> or <see cref="MediaKind.Image"/>.</summary>
    public static MediaKind FromFileName(string? fileNameOrUrl) =>
        IsVideo(fileNameOrUrl) ? MediaKind.Video : MediaKind.Image;
}
