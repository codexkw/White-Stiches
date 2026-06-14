namespace WhiteStiches.Core.Interfaces;

/// <summary>
/// Thrown by <see cref="IFileStorage.SaveAsync"/> when the storage root cannot be written to
/// (the folder is missing or the process has no write permission). On a deployed server this
/// almost always means Storage:Root is still the relative dev default ("../../storage") instead
/// of an absolute folder the app pool owns. Callers catch this to show a clear "storage isn't
/// writable" message and log the cause, rather than surfacing a raw 500.
/// </summary>
public sealed class StorageWriteException(string message, Exception inner) : Exception(message, inner);
