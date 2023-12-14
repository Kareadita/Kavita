using System.Globalization;
using System.Text.RegularExpressions;

namespace API.Extensions;
#nullable enable

public static class StringExtensions
{
    private static readonly Regex SentenceCaseRegex = new(@"(^[a-z])|\.\s+(.)",
        RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        Services.Tasks.Scanner.Parser.Parser.RegexTimeout);

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
        return string.IsNullOrEmpty(value) ? string.Empty : Services.Tasks.Scanner.Parser.Parser.Normalize(value);
    }

    public static float AsFloat(this string? value, float defaultValue = 0.0f)
    {
        return string.IsNullOrEmpty(value) ? defaultValue : float.Parse(value, CultureInfo.InvariantCulture);
    }

    public static double AsDouble(this string? value, double defaultValue = 0.0f)
    {
        return string.IsNullOrEmpty(value) ? defaultValue : double.Parse(value, CultureInfo.InvariantCulture);
    }
}
