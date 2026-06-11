namespace WhiteStiches.Web;

/// <summary>
/// Marker type for the storefront's shared string dictionary. Inject
/// <c>IStringLocalizer&lt;SharedResource&gt;</c> (aliased <c>L</c> in views) and key off the
/// English text — translations live in <c>Resources/SharedResource.ar.resx</c> (Phase 1E‑3).
/// </summary>
public sealed class SharedResource;
