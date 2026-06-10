using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Core.Interfaces.Admin;

/// <summary>
/// Staff and role management: list/create/disable staff accounts, role assignment
/// (AD-SET-04/06). Owned by the Settings admin module. Implementations enforce the
/// Super Admin guards (cannot lock the last SuperAdmin, cannot strip the last
/// SuperAdmin's SuperAdmin role, cannot lock yourself).
/// </summary>
public interface IStaffAdminService
{
    /// <summary>Every user holding at least one staff role.</summary>
    Task<IReadOnlyList<StaffMember>> GetStaffAsync(CancellationToken ct = default);

    Task<StaffMember?> GetStaffMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Creates a confirmed staff account with a temporary password and the given staff roles.</summary>
    Task<StaffOperationResult> CreateStaffAsync(string firstName, string lastName, string email,
        string password, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>Replaces the user's staff roles (never touches the Customer role).</summary>
    Task<StaffOperationResult> SetRolesAsync(Guid userId, IReadOnlyList<string> roles,
        Guid actingUserId, CancellationToken ct = default);

    /// <summary>Locks (indefinitely) or unlocks the account.</summary>
    Task<StaffOperationResult> SetLockAsync(Guid userId, bool locked, Guid actingUserId,
        CancellationToken ct = default);

    Task<StaffOperationResult> ResetPasswordAsync(Guid userId, string newPassword,
        CancellationToken ct = default);
}
