using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WhiteStiches.Core.Interfaces;
using WhiteStiches.Core.Utils;

namespace WhiteStiches.Infrastructure.Services;

public class FileStorageService(IConfiguration configuration, IHostEnvironment env) : IFileStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif", ".svg", ".mp4"
    };

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
        Directory.CreateDirectory(dir);

        await using (var fs = new FileStream(Path.Combine(dir, name), FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await content.CopyToAsync(fs, ct);
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
