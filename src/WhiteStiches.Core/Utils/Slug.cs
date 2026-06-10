using System.Text;

namespace WhiteStiches.Core.Utils;

public static class Slug
{
    /// <summary>Lowercase URL handle: latin letters/digits/dashes. Arabic and symbols are stripped.</summary>
    public static string Generate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        var lastDash = true;
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        return sb.ToString().Trim('-');
    }
}
