using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Models.Admin;
using WhiteStiches.Core.Utils;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Products back office: CRUD, images, options/variants, inventory (AD-PRD-01..05).</summary>
[Route("products")]
public class ProductsController(ICatalogService catalog, IFileStorage storage, IAuditService audit, IRichTextSanitizer sanitizer, ILogger<ProductsController> logger) : Controller
{
    private Guid? UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? UserName => User.Identity?.Name;

    // ------------------------------------------------------------------ list

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, ProductStatus? status, int? categoryId,
        int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Products";
        ViewData["Nav"] = "products";

        var products = await catalog.GetProductsAdminAsync(search, status, categoryId, page, 20, ct);
        var categories = await catalog.GetCategoriesAdminAsync(ct);

        return View(new ProductListViewModel
        {
            Products = products,
            Search = search,
            Status = status,
            CategoryId = categoryId,
            Categories = CategoryTree.BuildChoices(categories)
        });
    }

    // ------------------------------------------------------------------ create / edit form

    [HttpGet("new")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Title"] = "New product";
        ViewData["Nav"] = "products";

        var categories = await catalog.GetCategoriesAdminAsync(ct);
        return View("Edit", new ProductEditViewModel { Categories = CategoryTree.BuildChoices(categories) });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var product = await catalog.GetProductForEditAsync(id, ct);
        if (product is null)
        {
            TempData["Err"] = $"Product {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = product.TitleEn;
        ViewData["Nav"] = "products";

        var categories = await catalog.GetCategoriesAdminAsync(ct);
        return View(new ProductEditViewModel
        {
            Product = product,
            Form = ToForm(product),
            Categories = CategoryTree.BuildChoices(categories)
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProductFormModel form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.TitleEn))
        {
            TempData["Err"] = "Title (EN) is required.";
            return form.Id > 0
                ? RedirectToAction(nameof(Edit), new { id = form.Id })
                : RedirectToAction(nameof(Create));
        }

        var baseSlug = Slug.Generate(string.IsNullOrWhiteSpace(form.Slug) ? form.TitleEn : form.Slug);
        if (baseSlug.Length == 0) baseSlug = "product";

        if (form.Id == 0)
        {
            var product = new Product();
            Apply(form, product);
            product.Slug = await catalog.EnsureUniqueProductSlugAsync(baseSlug, null, ct);

            // Every product starts with exactly one default variant; the options form
            // regenerates the matrix once option axes are defined.
            product.Variants.Add(new ProductVariant
            {
                Price = 0m,
                StockQuantity = 0,
                IsActive = true,
                Position = 1
            });

            await catalog.CreateProductAsync(product, ct);
            await audit.LogAsync("product.create", UserId, UserName, nameof(Product), product.Id.ToString(),
                after: Snapshot(product));

            TempData["Ok"] = $"Product “{product.TitleEn}” created. Add images, options, and variants below.";
            return RedirectToAction(nameof(Edit), new { id = product.Id });
        }
        else
        {
            var product = await catalog.GetProductByIdAsync(form.Id, ct);
            if (product is null)
            {
                TempData["Err"] = $"Product {form.Id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            var before = Snapshot(product);
            Apply(form, product);
            product.Slug = await catalog.EnsureUniqueProductSlugAsync(baseSlug, product.Id, ct);

            await catalog.UpdateProductAsync(product, ct);
            await audit.LogAsync("product.update", UserId, UserName, nameof(Product), product.Id.ToString(),
                before: before, after: Snapshot(product));

            TempData["Ok"] = "Product saved.";
            return RedirectToAction(nameof(Edit), new { id = product.Id });
        }
    }

    // ------------------------------------------------------------------ images

    [HttpPost("{id:int}/images")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(64L * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(int id, List<IFormFile>? files, CancellationToken ct)
    {
        var product = await catalog.GetProductForEditAsync(id, ct);
        if (product is null)
        {
            TempData["Err"] = $"Product {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        var uploaded = 0;
        try
        {
            foreach (var file in files ?? [])
            {
                if (file.Length == 0) continue;

                await using var stream = file.OpenReadStream();
                var path = await storage.SaveAsync(stream, file.FileName, "products", ct);
                var kind = MediaKinds.FromFileName(file.FileName);
                var image = await catalog.AddProductImageAsync(id, path, kind, ct);
                await audit.LogAsync("product.image.add", UserId, UserName, nameof(ProductImage), image.Id.ToString(),
                    after: new { image.ProductId, image.Url, image.MediaKind, image.SortOrder });
                uploaded++;
            }
        }
        catch (StorageWriteException ex)
        {
            // Storage root unwritable (almost always a Storage:Root misconfig on the server). Show a
            // clear message and log the cause instead of returning an opaque 500.
            logger.LogError(ex, "Product image upload failed for product {ProductId} — media storage is not writable.", id);
            TempData["Err"] = uploaded > 0
                ? $"{uploaded} image(s) uploaded; the rest failed because media storage isn't writable on the server. Check the Storage:Root setting."
                : "Upload failed — media storage isn't writable on the server. Storage:Root must be an absolute folder the app pool can write to.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (InvalidOperationException ex)
        {
            // SaveAsync rejects a disallowed file type with this — surface its message to the user.
            logger.LogWarning(ex, "Product image upload rejected for product {ProductId}.", id);
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (uploaded > 0)
        {
            TempData["Ok"] = $"{uploaded} image(s) uploaded.";
        }
        else
        {
            TempData["Err"] = "No images selected — choose at least one file.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/images/{imageId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        var url = await catalog.DeleteProductImageAsync(id, imageId, ct);
        if (url is null)
        {
            TempData["Err"] = "Image was not found.";
        }
        else
        {
            await storage.DeleteAsync(url, ct);
            await audit.LogAsync("product.image.delete", UserId, UserName, nameof(ProductImage), imageId.ToString(),
                before: new { ProductId = id, Url = url });
            TempData["Ok"] = "Image removed.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/images/{imageId:int}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveImage(int id, int imageId, [FromQuery] string? dir, CancellationToken ct)
    {
        var up = string.Equals(dir, "up", StringComparison.OrdinalIgnoreCase);
        var down = string.Equals(dir, "down", StringComparison.OrdinalIgnoreCase);
        if (!up && !down)
        {
            TempData["Err"] = "Direction must be “up” or “down”.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await catalog.MoveProductImageAsync(id, imageId, up, ct);
        await audit.LogAsync("product.image.move", UserId, UserName, nameof(ProductImage), imageId.ToString(),
            after: new { ProductId = id, Direction = up ? "up" : "down" });

        TempData["Ok"] = "Image reordered.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/images/{imageId:int}/meta")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveImageMeta(int id, int imageId, string? altEn, string? altAr,
        string? colorName, CancellationToken ct)
    {
        await catalog.UpdateProductImageMetaAsync(id, imageId, altEn, altAr, colorName, ct);
        await audit.LogAsync("product.image.meta", UserId, UserName, nameof(ProductImage), imageId.ToString(),
            after: new { ProductId = id, AltEn = altEn, AltAr = altAr, ColorName = colorName });

        TempData["Ok"] = "Image details saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ------------------------------------------------------------------ options + variants

    [HttpPost("{id:int}/options")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOptions(int id, string? name1, string? values1, string? name2,
        string? values2, string? name3, string? values3, CancellationToken ct)
    {
        var product = await catalog.GetProductForEditAsync(id, ct);
        if (product is null)
        {
            TempData["Err"] = $"Product {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        var before = new
        {
            Options = product.Options.Select(o => new { o.Position, o.NameEn, o.ValuesCsv }).ToList(),
            VariantCount = product.Variants.Count
        };

        await catalog.SetProductOptionsAsync(id, new List<ProductOptionInput>
        {
            new(name1 ?? string.Empty, values1 ?? string.Empty),
            new(name2 ?? string.Empty, values2 ?? string.Empty),
            new(name3 ?? string.Empty, values3 ?? string.Empty)
        }, ct);

        var updated = await catalog.GetProductForEditAsync(id, ct);
        var after = new
        {
            Options = updated?.Options.Select(o => new { o.Position, o.NameEn, o.ValuesCsv }).ToList(),
            VariantCount = updated?.Variants.Count ?? 0
        };

        await audit.LogAsync("product.options.set", UserId, UserName, nameof(Product), id.ToString(),
            before: before, after: after);

        TempData["Ok"] = $"Options saved — variant matrix regenerated ({after.VariantCount} variant(s)).";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/variants")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVariants(int id, List<VariantUpdateRow> variants, CancellationToken ct)
    {
        var product = await catalog.GetProductForEditAsync(id, ct);
        if (product is null)
        {
            TempData["Err"] = $"Product {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        var before = product.Variants
            .Select(v => new { v.Id, v.Sku, v.Price, v.CompareAtPrice, v.StockQuantity, v.LowStockThreshold, v.AllowOversell, v.IsActive })
            .ToList();

        try
        {
            await catalog.UpdateVariantsAsync(id, variants, ct);
        }
        catch (DbUpdateException)
        {
            TempData["Err"] = "Variants not saved — SKUs must be unique across the store.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await audit.LogAsync("product.variants.update", UserId, UserName, nameof(Product), id.ToString(),
            before: before,
            after: variants.Select(v => new { v.Id, v.Sku, v.Price, v.CompareAtPrice, v.StockQuantity, v.LowStockThreshold, v.AllowOversell, v.IsActive }).ToList());

        TempData["Ok"] = "Variants saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ------------------------------------------------------------------ inventory

    [HttpGet("{id:int}/inventory")]
    public async Task<IActionResult> Inventory(int id, CancellationToken ct)
    {
        var product = await catalog.GetProductForEditAsync(id, ct);
        if (product is null)
        {
            TempData["Err"] = $"Product {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Inventory · {product.TitleEn}";
        ViewData["Nav"] = "products";

        var history = await catalog.GetInventoryHistoryAsync(id, 50, ct);
        return View(new ProductInventoryViewModel { Product = product, History = history });
    }

    [HttpPost("{id:int}/inventory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustInventory(int id, int variantId, int delta,
        InventoryAdjustmentReason reason, string? note, CancellationToken ct)
    {
        if (delta == 0)
        {
            TempData["Err"] = "Adjustment must be a non-zero quantity.";
            return RedirectToAction(nameof(Inventory), new { id });
        }

        var product = await catalog.GetProductForEditAsync(id, ct);
        var variant = product?.Variants.FirstOrDefault(v => v.Id == variantId);
        if (product is null || variant is null)
        {
            TempData["Err"] = "Variant does not belong to this product.";
            return RedirectToAction(nameof(Inventory), new { id });
        }

        await catalog.AdjustInventoryAsync(new InventoryAdjustment
        {
            ProductVariantId = variantId,
            QuantityDelta = delta,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            StaffUserId = UserId
        }, ct);

        await audit.LogAsync("inventory.adjust", UserId, UserName, nameof(ProductVariant), variantId.ToString(),
            before: new { variant.StockQuantity },
            after: new
            {
                StockQuantity = variant.StockQuantity + delta,
                Delta = delta,
                Reason = reason.ToString(),
                Note = note
            });

        TempData["Ok"] = $"Stock adjusted by {delta:+#;-#;0}.";
        return RedirectToAction(nameof(Inventory), new { id });
    }

    // ------------------------------------------------------------------ delete

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? confirm, CancellationToken ct)
    {
        if (!string.Equals(confirm?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Err"] = "Type “yes” in the confirmation box to delete. Setting status to Archived is usually the better option.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var product = await catalog.GetProductForEditAsync(id, ct);
        if (product is null)
        {
            TempData["Err"] = $"Product {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        var imageUrls = product.Images.Select(i => i.Url).ToList();
        var before = Snapshot(product);

        await catalog.DeleteProductAsync(id, ct);
        foreach (var url in imageUrls)
        {
            await storage.DeleteAsync(url, ct);
        }

        await audit.LogAsync("product.delete", UserId, UserName, nameof(Product), id.ToString(), before: before);

        TempData["Ok"] = $"Product “{product.TitleEn}” deleted. Past orders keep their line-item snapshots.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------ helpers

    private static ProductFormModel ToForm(Product p) => new()
    {
        Id = p.Id,
        TitleEn = p.TitleEn,
        TitleAr = p.TitleAr,
        Slug = p.Slug,
        DescriptionEn = p.DescriptionEn,
        DescriptionAr = p.DescriptionAr,
        MaterialCareEn = p.MaterialCareEn,
        MaterialCareAr = p.MaterialCareAr,
        SizeFitEn = p.SizeFitEn,
        SizeFitAr = p.SizeFitAr,
        SeoTitleEn = p.SeoTitleEn,
        SeoTitleAr = p.SeoTitleAr,
        SeoDescriptionEn = p.SeoDescriptionEn,
        SeoDescriptionAr = p.SeoDescriptionAr,
        Type = p.Type,
        Vendor = p.Vendor,
        Tags = p.Tags,
        Status = p.Status,
        PublishAtUtc = p.PublishAtUtc,
        IsFeatured = p.IsFeatured,
        CategoryId = p.CategoryId
    };

    private void Apply(ProductFormModel form, Product p)
    {
        p.TitleEn = form.TitleEn.Trim();
        p.TitleAr = form.TitleAr?.Trim() ?? string.Empty;
        // Rich-text fields are authored in the WYSIWYG and rendered raw on the storefront —
        // sanitize the HTML on the way in (Phase 1E‑2).
        p.DescriptionEn = sanitizer.Sanitize(form.DescriptionEn);
        p.DescriptionAr = sanitizer.Sanitize(form.DescriptionAr);
        p.MaterialCareEn = sanitizer.Sanitize(form.MaterialCareEn);
        p.MaterialCareAr = sanitizer.Sanitize(form.MaterialCareAr);
        p.SizeFitEn = sanitizer.Sanitize(form.SizeFitEn);
        p.SizeFitAr = sanitizer.Sanitize(form.SizeFitAr);
        p.SeoTitleEn = Clean(form.SeoTitleEn);
        p.SeoTitleAr = Clean(form.SeoTitleAr);
        p.SeoDescriptionEn = Clean(form.SeoDescriptionEn);
        p.SeoDescriptionAr = Clean(form.SeoDescriptionAr);
        p.Type = Clean(form.Type);
        p.Vendor = Clean(form.Vendor);
        p.Tags = Clean(form.Tags);
        p.Status = form.Status;
        p.PublishAtUtc = form.PublishAtUtc;
        p.IsFeatured = form.IsFeatured;
        p.CategoryId = form.CategoryId;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Product p) => new
    {
        p.Id,
        p.TitleEn,
        p.TitleAr,
        p.Slug,
        p.Type,
        p.Vendor,
        p.Tags,
        Status = p.Status.ToString(),
        p.PublishAtUtc,
        p.IsFeatured,
        p.CategoryId
    };
}
