namespace WhiteStiches.Core.Models.Admin;

/// <summary>
/// Outcome of a returns-queue transition (approve / reject / receive / refund).
/// Carries enough context for the controller to audit-log and toast without
/// re-querying. Owned by the Returns admin module (AD-ORD-10).
/// </summary>
public record ReturnActionResult(
    bool Success,
    string Message,
    int OrderId = 0,
    string? RmaNumber = null,
    string? OldStatus = null,
    string? NewStatus = null)
{
    public static ReturnActionResult Fail(string message) => new(false, message);
}
