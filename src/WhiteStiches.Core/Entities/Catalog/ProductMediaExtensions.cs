using WhiteStiches.Core.Enums;

namespace WhiteStiches.Core.Entities.Catalog;

/// <summary>
/// Thumbnail / card selection over a product's media. Everywhere a SINGLE representative image is
/// shown — product cards, cart, mini-cart, wishlist, search, checkout, order lines, admin lists —
/// must pick a still PHOTO, never a video; otherwise a clip sorted first would land a <c>.mp4</c>
/// inside an <c>&lt;img src&gt;</c> and render broken. The PDP gallery is the only surface that shows
/// the full media set (photos + video).
/// </summary>
public static class ProductMediaExtensions
{
    /// <summary>First still photo in display order; <c>null</c> when the product has no photos.</summary>
    public static ProductImage? PrimaryPhoto(this IEnumerable<ProductImage>? images) =>
        images?.Where(i => i.MediaKind == MediaKind.Image)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .FirstOrDefault();

    /// <summary>All still photos in display order (videos excluded).</summary>
    public static List<ProductImage> Photos(this IEnumerable<ProductImage>? images) =>
        images is null
            ? []
            : images.Where(i => i.MediaKind == MediaKind.Image)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .ToList();
}
