using System.Globalization;
using System.Text.RegularExpressions;

namespace API.Extensions;

public static class StringExtensions
{
    private static readonly Regex SentenceCaseRegex = new Regex(@"(^[a-z])|\.\s+(.)",
        RegexOptions.ExplicitCapture | RegexOptions.Compiled, Services.Tasks.Scanner.Parser.Parser.RegexTimeout);

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

    public static float AsFloat(this string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    public static double AsDouble(this string value)
    {
        return double.Parse(value, CultureInfo.InvariantCulture);
    }
}
