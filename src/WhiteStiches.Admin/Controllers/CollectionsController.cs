using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Controllers;

[Route("collections")]
public class CollectionsController(
    ICollectionAdminService collections,
    IFileStorage files,
    IAuditService audit,
    IRichTextSanitizer sanitizer) : Controller
{
    private const string NavKey = "collections";
    private const string UploadFolder = "collections";

    // ----------------------------------------------------------------- list

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        SetChrome("Collections");
        var list = await collections.GetListAsync(page, 20, ct);
        return View(new CollectionIndexViewModel { Collections = list });
    }

    // ----------------------------------------------------------------- form

    [HttpGet("new")]
    public IActionResult Create()
    {
        SetChrome("New collection");
        var vm = new CollectionEditViewModel { Form = { IsActive = true } };
        return View("Edit", vm);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, string? productSearch = null, CancellationToken ct = default)
    {
        var vm = await BuildEditViewModelAsync(id, productSearch, rulesOverride: null, ct);
        if (vm is null)
        {
            TempData["Err"] = "Collection not found.";
            return RedirectToAction(nameof(Index));
        }

        SetChrome($"Collection · {vm.Form.TitleEn}");
        return View(vm);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CollectionFormViewModel form, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(form.TitleEn))
        {
            TempData["Err"] = "Title (EN) is required.";
            return form.Id > 0
                ? RedirectToAction(nameof(Edit), new { id = form.Id })
                : RedirectToAction(nameof(Create));
        }

        Collection? existing = null;
        if (form.Id > 0)
        {
            existing = await collections.GetForEditAsync(form.Id, ct);
            if (existing is null)
            {
                TempData["Err"] = "Collection not found.";
                return RedirectToAction(nameof(Index));
            }
        }

        var entity = new Collection
        {
            Id = form.Id,
            TitleEn = form.TitleEn.Trim(),
            TitleAr = form.TitleAr?.Trim() ?? string.Empty,
            Slug = form.Slug?.Trim() ?? string.Empty,
            DescriptionEn = NullIfBlank(sanitizer.Sanitize(form.DescriptionEn)),
            DescriptionAr = NullIfBlank(sanitizer.Sanitize(form.DescriptionAr)),
            SortOrder = form.SortOrder,
            IsActive = form.IsActive,
            IsSmart = form.IsSmart,
            ShowInMenu = form.ShowInMenu,
            SeoTitleEn = NullIfBlank(form.SeoTitleEn),
            SeoTitleAr = NullIfBlank(form.SeoTitleAr),
            SeoDescriptionEn = NullIfBlank(form.SeoDescriptionEn),
            SeoDescriptionAr = NullIfBlank(form.SeoDescriptionAr),
            ImageUrl = existing?.ImageUrl,
            BannerUrl = existing?.BannerUrl,
            RulesJson = existing?.RulesJson
        };

        if (form.HeroImage is { Length: > 0 })
        {
            entity.ImageUrl = await files.SaveAsync(form.HeroImage.OpenReadStream(), form.HeroImage.FileName, UploadFolder, ct);
        }

        if (form.BannerImage is { Length: > 0 })
        {
            entity.BannerUrl = await files.SaveAsync(form.BannerImage.OpenReadStream(), form.BannerImage.FileName, UploadFolder, ct);
        }

        var before = existing is null
            ? null
            : new
            {
                existing.TitleEn, existing.TitleAr, existing.Slug, existing.IsActive, existing.IsSmart,
                existing.ShowInMenu, existing.SortOrder, existing.ImageUrl, existing.BannerUrl
            };

        var saved = await collections.SaveAsync(entity, ct);

        var after = new
        {
            saved.TitleEn, saved.TitleAr, saved.Slug, saved.IsActive, saved.IsSmart,
            saved.ShowInMenu, saved.SortOrder, saved.ImageUrl, saved.BannerUrl
        };

        await audit.LogAsync(existing is null ? "collection.create" : "collection.update",
            CurrentUserId(), CurrentUserName(), nameof(Collection), saved.Id.ToString(), before, after, ct: ct);

        TempData["Ok"] = existing is null
            ? $"Collection \"{saved.TitleEn}\" created."
            : $"Collection \"{saved.TitleEn}\" updated.";
        return RedirectToAction(nameof(Edit), new { id = saved.Id });
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var existing = await collections.GetForEditAsync(id, ct);
        if (existing is null)
        {
            TempData["Err"] = "Collection not found.";
            return RedirectToAction(nameof(Index));
        }

        var before = new { existing.TitleEn, existing.Slug, existing.IsSmart, ProductCount = existing.CollectionProducts.Count };
        await collections.DeleteAsync(id, ct);
        await audit.LogAsync("collection.delete",
            CurrentUserId(), CurrentUserName(), nameof(Collection), id.ToString(), before, null, ct: ct);

        TempData["Ok"] = $"Collection \"{existing.TitleEn}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------ manual curation

    [HttpPost("{id:int}/products/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(int id, int productId, string? productSearch = null, CancellationToken ct = default)
    {
        var added = await collections.AddProductAsync(id, productId, ct);
        if (added)
        {
            await audit.LogAsync("collection.product.add",
                CurrentUserId(), CurrentUserName(), nameof(Collection), id.ToString(),
                null, new { productId }, ct: ct);
            TempData["Ok"] = "Product added to collection.";
        }
        else
        {
            TempData["Err"] = "Product could not be added — it is missing or already in this collection.";
        }

        return RedirectToAction(nameof(Edit), new { id, productSearch });
    }

    [HttpPost("{id:int}/products/{productId:int}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProduct(int id, int productId, CancellationToken ct = default)
    {
        var removed = await collections.RemoveProductAsync(id, productId, ct);
        if (removed)
        {
            await audit.LogAsync("collection.product.remove",
                CurrentUserId(), CurrentUserName(), nameof(Collection), id.ToString(),
                new { productId }, null, ct: ct);
            TempData["Ok"] = "Product removed from collection.";
        }
        else
        {
            TempData["Err"] = "Product is not in this collection.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/products/{productId:int}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveProduct(int id, int productId, string? dir = null, CancellationToken ct = default)
    {
        var moveUp = string.Equals(dir, "up", StringComparison.OrdinalIgnoreCase);
        var moveDown = string.Equals(dir, "down", StringComparison.OrdinalIgnoreCase);
        if (!moveUp && !moveDown)
        {
            TempData["Err"] = "Direction must be up or down.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var moved = await collections.MoveProductAsync(id, productId, moveUp, ct);
        if (moved)
        {
            await audit.LogAsync("collection.product.move",
                CurrentUserId(), CurrentUserName(), nameof(Collection), id.ToString(),
                null, new { productId, dir = moveUp ? "up" : "down" }, ct: ct);
            TempData["Ok"] = "Sort order updated.";
        }
        else
        {
            TempData["Err"] = "Product could not be moved further.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    // ----------------------------------------------------------- smart rules

    [HttpPost("{id:int}/rules")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRules(int id, CollectionRulesFormViewModel form, string? addRow = null, CancellationToken ct = default)
    {
        form.Rules ??= [];

        // No-JS "Add rule": re-render the edit page with the posted (unsaved) rows plus one empty row.
        if (!string.IsNullOrEmpty(addRow))
        {
            if (form.Rows > 50) form.Rows = 50;
            while (form.Rules.Count < form.Rows)
            {
                form.Rules.Add(new CollectionRuleRowViewModel());
            }

            form.Rules.Add(new CollectionRuleRowViewModel());
            form.Rows = form.Rules.Count;

            var vm = await BuildEditViewModelAsync(id, productSearch: null, rulesOverride: form, ct);
            if (vm is null)
            {
                TempData["Err"] = "Collection not found.";
                return RedirectToAction(nameof(Index));
            }

            SetChrome($"Collection · {vm.Form.TitleEn}");
            return View("Edit", vm);
        }

        var existing = await collections.GetForEditAsync(id, ct);
        if (existing is null)
        {
            TempData["Err"] = "Collection not found.";
            return RedirectToAction(nameof(Index));
        }

        var ruleSet = new CollectionRuleSet
        {
            Match = string.Equals(form.Match, CollectionRuleSet.MatchAny, StringComparison.OrdinalIgnoreCase)
                ? CollectionRuleSet.MatchAny
                : CollectionRuleSet.MatchAll,
            Rules = form.Rules
                .Where(r => r is not null
                            && !string.IsNullOrWhiteSpace(r.Value)
                            && CollectionRuleSet.AllowedFields.Contains(r.Field, StringComparer.OrdinalIgnoreCase)
                            && CollectionRuleSet.AllowedOperators.Contains(r.Operator, StringComparer.OrdinalIgnoreCase))
                .Select(r => new CollectionRule
                {
                    Field = r.Field.ToLowerInvariant(),
                    Operator = r.Operator.ToLowerInvariant(),
                    Value = r.Value.Trim()
                })
                .ToList()
        };

        var before = new { existing.RulesJson };
        await collections.SaveRulesAsync(id, ruleSet, ct);
        await audit.LogAsync("collection.rules.save",
            CurrentUserId(), CurrentUserName(), nameof(Collection), id.ToString(),
            before, ruleSet, ct: ct);

        TempData["Ok"] = $"Smart rules saved ({ruleSet.Rules.Count} rule{(ruleSet.Rules.Count == 1 ? "" : "s")}).";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/rules/apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyRules(int id, CancellationToken ct = default)
    {
        var existing = await collections.GetForEditAsync(id, ct);
        if (existing is null)
        {
            TempData["Err"] = "Collection not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!existing.IsSmart || CollectionRuleSet.Parse(existing.RulesJson) is null)
        {
            TempData["Err"] = "Save smart rules before applying them.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var before = new { ProductCount = existing.CollectionProducts.Count };
        var count = await collections.ApplyRulesAsync(id, ct);
        await audit.LogAsync("collection.rules.apply",
            CurrentUserId(), CurrentUserName(), nameof(Collection), id.ToString(),
            before, new { ProductCount = count }, ct: ct);

        TempData["Ok"] = $"Rules applied — collection now contains {count} product{(count == 1 ? "" : "s")}.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ---------------------------------------------------------------- helpers

    private void SetChrome(string title)
    {
        ViewData["Title"] = title;
        ViewData["Nav"] = NavKey;
    }

    private Guid? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private string? CurrentUserName() => User.Identity?.Name;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<CollectionEditViewModel?> BuildEditViewModelAsync(int id, string? productSearch,
        CollectionRulesFormViewModel? rulesOverride, CancellationToken ct)
    {
        var entity = await collections.GetForEditAsync(id, ct);
        if (entity is null) return null;

        var vm = new CollectionEditViewModel
        {
            Form = new CollectionFormViewModel
            {
                Id = entity.Id,
                TitleEn = entity.TitleEn,
                TitleAr = entity.TitleAr,
                Slug = entity.Slug,
                DescriptionEn = entity.DescriptionEn,
                DescriptionAr = entity.DescriptionAr,
                SortOrder = entity.SortOrder,
                IsActive = entity.IsActive,
                IsSmart = entity.IsSmart,
                ShowInMenu = entity.ShowInMenu,
                SeoTitleEn = entity.SeoTitleEn,
                SeoTitleAr = entity.SeoTitleAr,
                SeoDescriptionEn = entity.SeoDescriptionEn,
                SeoDescriptionAr = entity.SeoDescriptionAr,
                ImageUrl = entity.ImageUrl,
                BannerUrl = entity.BannerUrl
            },
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Products = entity.CollectionProducts
                .OrderBy(cp => cp.Position)
                .Select(cp => new CollectionProductRowViewModel
                {
                    ProductId = cp.ProductId,
                    TitleEn = cp.Product.TitleEn,
                    Slug = cp.Product.Slug,
                    Type = cp.Product.Type,
                    ImageUrl = cp.Product.Images.PrimaryPhoto()?.Url,
                    Price = cp.Product.Variants.Where(v => v.IsActive).OrderBy(v => v.Position)
                        .Select(v => (decimal?)v.Price).FirstOrDefault(),
                    Position = cp.Position
                })
                .ToList(),
            ProductSearch = productSearch
        };

        // Manual picker: only when the search form was submitted (productSearch present in the
        // query). Empty-string values bind to null, so also check the raw query for the key.
        var pickerRequested = productSearch is not null || Request.Query.ContainsKey("productSearch");
        if (!entity.IsSmart && pickerRequested)
        {
            vm.ShowPickerResults = true;
            var results = await collections.SearchProductsForPickerAsync(id, productSearch, 20, ct);
            vm.PickerResults = results.Select(p => new CollectionPickerResultViewModel
            {
                ProductId = p.Id,
                TitleEn = p.TitleEn,
                Slug = p.Slug,
                Type = p.Type,
                ImageUrl = p.Images.PrimaryPhoto()?.Url,
                Price = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Position)
                    .Select(v => (decimal?)v.Price).FirstOrDefault()
            }).ToList();
        }

        // Smart rules: posted values win (Add rule re-render); otherwise load from RulesJson.
        var savedRules = CollectionRuleSet.Parse(entity.RulesJson);
        vm.HasSavedRules = savedRules is not null && savedRules.Rules.Count > 0;

        if (rulesOverride is not null)
        {
            vm.RulesForm = rulesOverride;
        }
        else if (savedRules is not null)
        {
            vm.RulesForm = new CollectionRulesFormViewModel
            {
                Match = savedRules.Match,
                Rules = savedRules.Rules.Select(r => new CollectionRuleRowViewModel
                {
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList()
            };
        }

        if (vm.RulesForm.Rules.Count == 0)
        {
            vm.RulesForm.Rules.Add(new CollectionRuleRowViewModel());
        }

        vm.RulesForm.Rows = vm.RulesForm.Rules.Count;

        if (entity.IsSmart && vm.HasSavedRules)
        {
            var matched = await collections.EvaluateRulesAsync(id, ct);
            vm.PreviewCount = matched.Count;
            vm.PreviewTitles = matched.Take(8).Select(p => p.TitleEn).ToList();
        }

        return vm;
    }
}
