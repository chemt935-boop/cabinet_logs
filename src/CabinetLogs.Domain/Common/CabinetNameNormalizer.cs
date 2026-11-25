using System.Text.RegularExpressions;

namespace CabinetLogs.Domain.Common;

public static partial class CabinetNameNormalizer
{
    /// <summary>
    /// Cleans display name by trimming outer whitespace and collapsing consecutive internal whitespace to a single space.
    /// </summary>
    public static string CleanDisplayName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return MultiWhitespaceRegex().Replace(input.Trim(), " ");
    }

    /// <summary>
    /// Produces a canonical case-insensitive key for deduplicating cabinet names.
    /// Example: "  LAF-01  " and "laf-01" both normalize to "laf-01".
    /// </summary>
    public static string Normalize(string input)
    {
        return CleanDisplayName(input).ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
