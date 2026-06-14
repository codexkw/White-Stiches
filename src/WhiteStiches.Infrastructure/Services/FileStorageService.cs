using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WhiteStiches.Core.Enums;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Utils;

namespace WhiteStiches.Infrastructure.Services;

public class FileStorageService(IConfiguration configuration, IHostEnvironment env) : IFileStorage
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif", ".svg"
    };

    // Images plus the shared video set (MediaKinds.VideoExtensions) so the uploader and the
    // img-vs-video render decision can never disagree about which formats count as video.
    private static readonly HashSet<string> AllowedExtensions =
        new(ImageExtensions.Concat(MediaKinds.VideoExtensions), StringComparer.OrdinalIgnoreCase);

    private string Root => StorageSetup.ResolveStorageRoot(configuration, env);

    public async Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException($"File type '{ext}' is not allowed.");
        }

        var safeFolder = NormalizeFolder(folder);
        var baseName = Slug.Generate(Path.GetFileNameWithoutExtension(fileName));
        if (baseName.Length == 0) baseName = "file";
        if (baseName.Length > 40) baseName = baseName[..40];

        var name = $"{baseName}-{Guid.NewGuid().ToString("N")[..8]}{ext.ToLowerInvariant()}";
        var dir = Path.Combine(Root, safeFolder.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            Directory.CreateDirectory(dir);

            await using var fs = new FileStream(Path.Combine(dir, name), FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            await content.CopyToAsync(fs, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Folder missing or no write permission — surface a clear, catchable reason (with the
            // resolved root) so callers can show a friendly message instead of a raw 500. Cancellation
            // (OperationCanceledException) is intentionally not caught here so it propagates normally.
            throw new StorageWriteException(
                $"Could not write the upload to '{dir}'. Ensure Storage:Root is an absolute path the app pool can read/write " +
                $"(it currently resolves to '{Root}').", ex);
        }

        return $"/media/{safeFolder}/{name}";
    }

    public Task DeleteAsync(string webPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webPath) ||
            !webPath.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask; // not managed by us (e.g. seeded /assets paths)
        }

        var relative = webPath["/media/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(Root, relative));
        if (!fullPath.StartsWith(Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask; // traversal guard
        }

        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private static string NormalizeFolder(string folder)
    {
        var parts = (folder ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Slug.Generate)
            .Where(p => p.Length > 0)
            .ToArray();

        return parts.Length == 0 ? "misc" : string.Join('/', parts);
    }
}
