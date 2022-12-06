using System.Text.RegularExpressions;

namespace API.Extensions;

public static partial class StringExtensions
{
    [GeneratedRegex(@"(^[a-z])|\.\s+(.)", RegexOptions.ExplicitCapture | RegexOptions.Compiled)]
    private static partial Regex SentenceCaseRegex();

    public static string SentenceCase(this string value)
    {
        return SentenceCaseRegex().Replace(value.ToLower(), s => s.Value.ToUpper());
    }

    /// <summary>
    /// Apply normalization on the String
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string ToNormalized(this string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return Services.Tasks.Scanner.Parser.Parser.Normalize(value);
    }
}
