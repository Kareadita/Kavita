using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace API.Extensions;
#nullable enable

public static partial class StringExtensions
{
    private static readonly Regex SentenceCaseRegex = new(@"(^[a-z])|\.\s+(.)",
        RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        Services.Tasks.Scanner.Parser.Parser.RegexTimeout);

    public static string Sanitize(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove all newline and control characters
        var sanitized = input
            .Replace(Environment.NewLine, string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\r", string.Empty);

        // Optionally remove other potentially unwanted characters
        sanitized = Regex.Replace(sanitized, @"[^\u0020-\u007E]", string.Empty); // Removes non-printable ASCII

        return sanitized.Trim(); // Trim any leading/trailing whitespace
    }

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

    public static string TrimPrefix(this string? value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (!value.StartsWith(prefix)) return value;

        return value.Substring(prefix.Length);
    }

    /// <summary>
    /// Censor the input string by removing all but the first and last char.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <remarks>If the input is an email (contains @), the domain will remain untouched</remarks>
    public static string Censor(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;

        var atIdx = input.IndexOf('@');
        if (atIdx == -1)
        {
            return $"{input[0]}{new string('*', input.Length - 1)}";
        }

        return input[0] + new string('*', atIdx - 1) + input[atIdx..];
    }

    /// <summary>
    /// Repeat returns a string that is equal to the original string repeat n times
    /// </summary>
    /// <param name="input">String to repeat</param>
    /// <param name="n">Amount of times to repeat</param>
    /// <returns></returns>
    public static string Repeat(this string? input, int n)
    {
        return string.IsNullOrEmpty(input) ? string.Empty : string.Concat(Enumerable.Repeat(input, n));
    }

    public static IList<int> ParseIntArray(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToList();
    }

    /// <summary>
    /// Parses a human-readable file size string (e.g. "1.43 GB") into bytes.
    /// </summary>
    /// <param name="input">The input string like "1.43 GB", "4.2 KB", "512 B"</param>
    /// <returns>Byte count as long</returns>
    public static long ParseHumanReadableBytes(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or empty.", nameof(input));
        }


        var match = HumanReadableBytesRegex().Match(input);
        if (!match.Success)
        {
            throw new FormatException($"Invalid format: '{input}'");
        }


        var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value.ToUpperInvariant();

        var multiplier = unit switch
        {
            "B" => 1L,
            "KB" => 1L << 10,
            "MB" => 1L << 20,
            "GB" => 1L << 30,
            "TB" => 1L << 40,
            "PB" => 1L << 50,
            "EB" => 1L << 60,
            _ => throw new FormatException($"Unknown unit: '{unit}'")
        };

        return (long)(value * multiplier);
    }

    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)?)\s*([KMGTPE]?B)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HumanReadableBytesRegex();
}
