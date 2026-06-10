namespace WhiteStiches.Core.Models.Admin;

/// <summary>
/// Identity-free staff projection for the Admin back office (AD-SET-02).
/// Keeps Core ignorant of ApplicationUser / ASP.NET Identity types.
/// </summary>
public record StaffMember(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string Email,
    IReadOnlyList<string> Roles,
    bool TwoFactorEnabled,
    bool IsLockedOut,
    DateTime? LastLoginAtUtc)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Outcome of a staff mutation. <see cref="UserId"/> is set on successful creation.</summary>
public record StaffOperationResult(bool Ok, IReadOnlyList<string> Errors, Guid? UserId = null)
{
    public static StaffOperationResult Success(Guid? userId = null) => new(true, [], userId);

    public static StaffOperationResult Fail(params string[] errors) => new(false, errors);

    public string ErrorMessage => string.Join(" ", Errors);
}
