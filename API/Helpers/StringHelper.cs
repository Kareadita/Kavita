using System.Text.RegularExpressions;

namespace API.Helpers;
#nullable enable

public static class StringHelper
{
    /// <summary>
    /// Used to squash duplicate break and new lines with a single new line.
    /// </summary>
    /// <example>Test br br Test -> Test br Test</example>
    /// <param name="summary"></param>
    /// <returns></returns>
    public static string? SquashBreaklines(string? summary)
    {
        // TODO: Test this as it's not reliable
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null; // Return as is if null, empty, or whitespace.
        }

        // Remove all variations of <br> tags (case-insensitive)
        summary = Regex.Replace(summary, @"<br\s*/?>", " ", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Normalize whitespace (replace multiple spaces with a single space)
        summary = Regex.Replace(summary, @"\s+", " ").Trim();

        return summary;
    }
}
