using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiteStiches.Admin.Models;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Admin.Controllers;

/// <summary>Homepage hero banner CMS (Wave 4 #1): bilingual copy, video/image background, stat counters.</summary>
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.Admin)]
[Route("banners")]
public class BannersController(IBannerAdminService banners, IFileStorage storage, IAuditService audit) : Controller
{
    private const string NavKey = "banners";
    private const string UploadFolder = "banners";

    /// <summary>How many stat-counter slots the edit form exposes (blank ones are dropped on save).</summary>
    private const int StatSlots = 4;

    private Guid? UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? UserName => User.Identity?.Name;

    // ------------------------------------------------------------------ list

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Banners";
        ViewData["Nav"] = NavKey;
        var list = await banners.GetListAsync(page, 20, ct);
        return View(new BannerIndexViewModel { Banners = list });
    }

    // ------------------------------------------------------------------ form

    [HttpGet("new")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New banner";
        ViewData["Nav"] = NavKey;

        var vm = new BannerEditViewModel
        {
            Form = new BannerFormViewModel
            {
                IsActive = true,
                ShowStats = true,
                TitleLine2Italic = true,
                Stats = PadStats([])
            }
        };
        return View("Edit", vm);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var banner = await banners.GetForEditAsync(id, ct);
        if (banner is null)
        {
            TempData["Err"] = "Banner not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"Banner · {banner.AdminLabel}";
        ViewData["Nav"] = NavKey;
        return View(BuildEditViewModel(banner));
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BannerFormViewModel form, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(form.TitleLine1En))
        {
            TempData["Err"] = "Title line 1 (EN) is required.";
            return form.Id > 0
                ? RedirectToAction(nameof(Edit), new { id = form.Id })
                : RedirectToAction(nameof(Create));
        }

        var entity = new Banner
        {
            Id = form.Id,
            AdminLabel = string.IsNullOrWhiteSpace(form.AdminLabel) ? form.TitleLine1En.Trim() : form.AdminLabel.Trim(),
            EyebrowEn = NullIfBlank(form.EyebrowEn),
            EyebrowAr = NullIfBlank(form.EyebrowAr),
            TitleLine1En = form.TitleLine1En.Trim(),
            TitleLine1Ar = NullIfBlank(form.TitleLine1Ar),
            TitleLine2En = NullIfBlank(form.TitleLine2En),
            TitleLine2Ar = NullIfBlank(form.TitleLine2Ar),
            TitleLine2Italic = form.TitleLine2Italic,
            LedeEn = NullIfBlank(form.LedeEn),
            LedeAr = NullIfBlank(form.LedeAr),
            PrimaryCtaTextEn = NullIfBlank(form.PrimaryCtaTextEn),
            PrimaryCtaTextAr = NullIfBlank(form.PrimaryCtaTextAr),
            PrimaryCtaUrl = NullIfBlank(form.PrimaryCtaUrl),
            SecondaryCtaTextEn = NullIfBlank(form.SecondaryCtaTextEn),
            SecondaryCtaTextAr = NullIfBlank(form.SecondaryCtaTextAr),
            SecondaryCtaUrl = NullIfBlank(form.SecondaryCtaUrl),
            IsActive = form.IsActive,
            ShowStats = form.ShowStats,
            SortOrder = form.SortOrder
        };

        // Keep only stat rows that carry a value or a label; everything else is an empty slot.
        var stats = (form.Stats ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Value)
                        || !string.IsNullOrWhiteSpace(s.LabelEn)
                        || !string.IsNullOrWhiteSpace(s.LabelAr))
            .Select(s => new BannerStat
            {
                Value = s.Value?.Trim() ?? string.Empty,
                LabelEn = NullIfBlank(s.LabelEn),
                LabelAr = NullIfBlank(s.LabelAr),
                IsVisible = s.IsVisible
            })
            .ToList();

        var saved = await banners.SaveAsync(entity, stats, ct);
        await audit.LogAsync(form.Id == 0 ? "banner.create" : "banner.update", UserId, UserName,
            nameof(Banner), saved.Id.ToString(),
            after: new { saved.AdminLabel, saved.IsActive, saved.ShowStats, saved.SortOrder, StatCount = stats.Count });

        TempData["Ok"] = form.Id == 0 ? "Banner created." : "Banner updated.";
        return RedirectToAction(nameof(Edit), new { id = saved.Id });
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var banner = await banners.GetForEditAsync(id, ct);
        var urls = banner?.Images.Select(i => i.Url).ToList() ?? [];

        var ok = await banners.DeleteAsync(id, ct);
        if (!ok)
        {
            TempData["Err"] = "Banner not found.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var url in urls) await storage.DeleteAsync(url, ct);
        await audit.LogAsync("banner.delete", UserId, UserName, nameof(Banner), id.ToString());
        TempData["Ok"] = "Banner deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct = default)
    {
        var state = await banners.ToggleActiveAsync(id, ct);
        if (state is null)
        {
            TempData["Err"] = "Banner not found.";
        }
        else
        {
            await audit.LogAsync("banner.toggle", UserId, UserName, nameof(Banner), id.ToString(),
                after: new { IsActive = state.Value });
            TempData["Ok"] = state.Value ? "Banner published." : "Banner hidden (draft).";
        }

        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------ media

    [HttpPost("{id:int}/images")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(64L * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(int id, List<IFormFile>? files, CancellationToken ct)
    {
        var banner = await banners.GetForEditAsync(id, ct);
        if (banner is null)
        {
            TempData["Err"] = $"Banner {id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        var uploaded = 0;
        foreach (var file in files ?? [])
        {
            if (file.Length == 0) continue;

            await using var stream = file.OpenReadStream();
            var path = await storage.SaveAsync(stream, file.FileName, UploadFolder, ct);
            var kind = MediaKinds.FromFileName(file.FileName);
            var image = await banners.AddImageAsync(id, path, kind, ct);
            if (image is null) continue;

            await audit.LogAsync("banner.image.add", UserId, UserName, nameof(BannerImage), image.Id.ToString(),
                after: new { image.BannerId, image.Url, image.MediaKind });
            uploaded++;
        }

        TempData[uploaded > 0 ? "Ok" : "Err"] = uploaded > 0
            ? $"{uploaded} file(s) uploaded."
            : "No files selected — choose at least one image or video.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/images/{imageId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        var url = await banners.DeleteImageAsync(id, imageId, ct);
        if (url is null)
        {
            TempData["Err"] = "Media was not found.";
        }
        else
        {
            await storage.DeleteAsync(url, ct);
            await audit.LogAsync("banner.image.delete", UserId, UserName, nameof(BannerImage), imageId.ToString(),
                before: new { BannerId = id, Url = url });
            TempData["Ok"] = "Media removed.";
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

        await banners.MoveImageAsync(id, imageId, up, ct);
        TempData["Ok"] = "Media reordered.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ------------------------------------------------------------------ helpers

    private BannerEditViewModel BuildEditViewModel(Banner banner) => new()
    {
        Form = new BannerFormViewModel
        {
            Id = banner.Id,
            AdminLabel = banner.AdminLabel,
            EyebrowEn = banner.EyebrowEn,
            EyebrowAr = banner.EyebrowAr,
            TitleLine1En = banner.TitleLine1En,
            TitleLine1Ar = banner.TitleLine1Ar,
            TitleLine2En = banner.TitleLine2En,
            TitleLine2Ar = banner.TitleLine2Ar,
            TitleLine2Italic = banner.TitleLine2Italic,
            LedeEn = banner.LedeEn,
            LedeAr = banner.LedeAr,
            PrimaryCtaTextEn = banner.PrimaryCtaTextEn,
            PrimaryCtaTextAr = banner.PrimaryCtaTextAr,
            PrimaryCtaUrl = banner.PrimaryCtaUrl,
            SecondaryCtaTextEn = banner.SecondaryCtaTextEn,
            SecondaryCtaTextAr = banner.SecondaryCtaTextAr,
            SecondaryCtaUrl = banner.SecondaryCtaUrl,
            IsActive = banner.IsActive,
            ShowStats = banner.ShowStats,
            SortOrder = banner.SortOrder,
            Stats = PadStats(banner.Stats
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Select(s => new BannerStatRow
                {
                    Value = s.Value,
                    LabelEn = s.LabelEn,
                    LabelAr = s.LabelAr,
                    IsVisible = s.IsVisible
                }))
        },
        CreatedAtUtc = banner.CreatedAtUtc,
        UpdatedAtUtc = banner.UpdatedAtUtc,
        Images = banner.Images
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new BannerImageRow
            {
                Id = i.Id,
                Url = i.Url,
                MediaKind = i.MediaKind,
                AltEn = i.AltEn,
                AltAr = i.AltAr,
                SortOrder = i.SortOrder
            })
            .ToList()
    };

    /// <summary>Pads (or trims) the stat rows to a fixed number of editable slots.</summary>
    private static List<BannerStatRow> PadStats(IEnumerable<BannerStatRow> rows)
    {
        var list = rows.Take(StatSlots).ToList();
        while (list.Count < StatSlots) list.Add(new BannerStatRow { IsVisible = true });
        return list;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
