using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WhiteStiches.Infrastructure;

/// <summary>
/// Shared upload storage lives OUTSIDE both apps' wwwroot (Storage:Root) so the
/// Admin can write files the Web app serves at /media. For a deployed environment
/// (e.g. IIS) set Storage:Root to an ABSOLUTE path both app pools can read/write —
/// a relative default is only meant for local development.
/// </summary>
public static class StorageSetup
{
    public static string ResolveStorageRoot(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration["Storage:Root"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Dev default: solution-level /storage (two levels up from src/<App>).
            configured = "../../storage";
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
    }

    /// <summary>
    /// Serves the shared storage folder at /media. Call in both apps after MapStaticAssets.
    /// Never throws on startup: if the folder can't be created (e.g. the relative default
    /// escapes the site root into a path the app pool can't write), it logs and skips
    /// serving /media rather than taking the whole app down with a 500.30.
    /// </summary>
    public static IApplicationBuilder UseWhiteStichesMedia(this IApplicationBuilder app, IConfiguration configuration, IHostEnvironment env)
    {
        var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger("WhiteStiches.Media");
        var root = ResolveStorageRoot(configuration, env);

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "Media storage root '{Root}' is not usable. File uploads and /media serving are disabled until " +
                "'Storage:Root' points to an absolute folder the app pool can write to. The app will still start.",
                root);
            return app;
        }

        logger?.LogInformation("Serving uploaded media from '{Root}' at /media.", root);

        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = "/media"
        });
    }
}
