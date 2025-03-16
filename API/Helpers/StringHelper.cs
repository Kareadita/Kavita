using System.Text.RegularExpressions;

namespace API.Helpers;
#nullable enable

public static partial class StringHelper
{
    #region Regex Source Generators
    [GeneratedRegex(@"\s?\(Source:\s*[^)]+\)")]
    private static partial Regex SourceRegex();
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex BrStandardizeRegex();
    [GeneratedRegex(@"(?:<br />\s*)+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex BrMultipleRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
    #endregion

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
        summary = BrStandardizeRegex().Replace(summary, "<br />");

        // Replace multiple consecutive br tags with a single br tag
        summary = BrMultipleRegex().Replace(summary, "<br /> ");

        // Normalize remaining whitespace (replace multiple spaces with a single space)
        summary = WhiteSpaceRegex().Replace(summary, " ").Trim();

        return summary.Trim();
    }

    /// <summary>
    /// Removes the (Source: MangaDex) type of tags at the end of descriptions from AL
    /// </summary>
    /// <param name="description"></param>
    /// <returns></returns>
    public static string? RemoveSourceInDescription(string? description)
    {
        if (string.IsNullOrEmpty(description)) return description;

        return SourceRegex().Replace(description, string.Empty).Trim();
    }
}
