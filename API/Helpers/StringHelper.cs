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
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        // First standardize all br tags to <br /> format
        summary = Regex.Replace(summary, @"<br\s*/?>", "<br />", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Replace multiple consecutive br tags with a single br tag
        summary = Regex.Replace(summary, @"(?:<br />\s*)+", "<br /> ", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Normalize remaining whitespace (replace multiple spaces with a single space)
        summary = Regex.Replace(summary, @"\s+", " ").Trim();

        return summary.Trim();
    }

    /// <summary>
    /// Removes the (Source: MangaDex) type of tags at the end of descriptions from AL
    /// </summary>
    /// <param name="description"></param>
    /// <returns></returns>
    public static string? RemoveSourceInDescription(string? description)
    {
        return description?.Trim();
    }
}
