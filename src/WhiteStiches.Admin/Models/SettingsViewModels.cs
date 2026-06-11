using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WhiteStiches.Core.Entities.Settings;
using WhiteStiches.Core.Models;
using WhiteStiches.Core.Models.Admin;

namespace WhiteStiches.Admin.Models;

// ---------------------------------------------------------------- Settings

public class SettingsIndexViewModel
{
    public IReadOnlyDictionary<string, string?> Values { get; init; } =
        new Dictionary<string, string?>();

    public string Get(string key) => Values.GetValueOrDefault(key) ?? string.Empty;

    /// <summary>Renders a stored decimal setting as KWD with 3 decimals; raw value if unparseable.</summary>
    public string GetMoney(string key)
    {
        var raw = Get(key);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("0.000", CultureInfo.InvariantCulture)
            : raw;
    }

    public bool GetBool(string key) =>
        string.Equals(Get(key), "true", StringComparison.OrdinalIgnoreCase);
}

// ---------------------------------------------------------------- Staff

public class StaffListViewModel
{
    public PagedResult<StaffMember> Members { get; init; } = new();
}

public class StaffNewViewModel
{
    public IReadOnlyList<string> StaffRoles { get; init; } = [];
    public IReadOnlyDictionary<string, string> RoleDescriptions { get; init; } =
        new Dictionary<string, string>();
}

public class StaffEditViewModel
{
    public required StaffMember Member { get; init; }
    public IReadOnlyList<string> StaffRoles { get; init; } = [];
    public IReadOnlyDictionary<string, string> RoleDescriptions { get; init; } =
        new Dictionary<string, string>();
    public bool IsSelf { get; init; }
}

// ---------------------------------------------------------------- Audit

public class AuditListViewModel
{
    public required PagedResult<AuditLogEntry> Entries { get; init; }
    public string? ActionFilter { get; init; }
}

// ---------------------------------------------------------------- Profile / 2FA

public class TwoFactorStatusViewModel
{
    public bool Enabled { get; init; }
    public int RecoveryCodesLeft { get; init; }
}

public class TwoFactorSetupViewModel
{
    public required string SharedKey { get; init; }
    public required string OtpAuthUri { get; init; }
}

public class RecoveryCodesViewModel
{
    public IReadOnlyList<string> Codes { get; init; } = [];
}

// ---------------------------------------------------------------- Auth 2FA step

public class LoginTwoFactorViewModel
{
    [Required(ErrorMessage = "Enter the 6-digit code from your authenticator app.")]
    public string Code { get; set; } = string.Empty;

    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
