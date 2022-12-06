using System;
using System.Text.RegularExpressions;

namespace API.Extensions;

public static class StringExtensions
{
    private static readonly Regex SentenceCaseRegex = new Regex(@"(^[a-z])|\.\s+(.)", RegexOptions.ExplicitCapture | RegexOptions.Compiled);

    public static string SentenceCase(this string value)
    {
        return SentenceCaseRegex.Replace(value.ToLower(), s => s.Value.ToUpper());
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
