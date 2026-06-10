namespace WhiteStiches.Core.Interfaces;

/// <summary>
/// Stores uploaded files under the shared storage root (Storage:Root, solution-level
/// /storage by default). Both apps serve that folder at /media, so a path returned
/// here renders identically on the storefront and in the back office.
/// </summary>
public interface IFileStorage
{
    /// <summary>Saves and returns the web path (e.g. "/media/products/atlas-1a2b3c4d.jpg").</summary>
    Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default);

    /// <summary>Deletes a previously saved file by its /media web path. Non-/media paths (seeded /assets images) are ignored.</summary>
    Task DeleteAsync(string webPath, CancellationToken ct = default);
}
