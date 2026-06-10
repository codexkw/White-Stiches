using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace WhiteStiches.Infrastructure;

/// <summary>
/// Shared upload storage lives OUTSIDE both apps' wwwroot (Storage:Root, default
/// solution-level /storage) so the Admin can write files the Web app serves.
/// </summary>
public static class StorageSetup
{
    public static string ResolveStorageRoot(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration["Storage:Root"] ?? "../../storage";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
    }

    /// <summary>Serves the shared storage folder at /media. Call in both apps after MapStaticAssets.</summary>
    public static IApplicationBuilder UseWhiteStichesMedia(this IApplicationBuilder app, IConfiguration configuration, IHostEnvironment env)
    {
        var root = ResolveStorageRoot(configuration, env);
        Directory.CreateDirectory(root);

        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = "/media"
        });
    }
}
