using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kavita.Common.Extensions;
#nullable enable

public static partial class StringExtensions
{
    private static readonly Regex SentenceCaseRegex = new(@"(^[a-z])|\.\s+(.)",
        RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    /// <summary>
    /// Normalize everything within Kavita. Some characters don't fall under Unicode, like full-width characters and need to be
    /// added on a case-by-case basis.
    /// </summary>
    private static readonly Regex NormalizeRegex = new(@"[^\p{L}0-9\+!＊！＋]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));

    extension(string input)
    {
        public string Sanitize()
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

        public string SentenceCase()
        {
            return SentenceCaseRegex.Replace(input.ToLower(), s => s.Value.ToUpper());
        }
    }

    /// <param name="value"></param>
    extension(string? value)
    {
        /// <summary>
        /// Apply normalization on the String
        /// </summary>
        /// <returns></returns>
        public string ToNormalized()
        {
            return string.IsNullOrEmpty(value) ? string.Empty : NormalizeRegex.Replace(value, string.Empty).Trim().ToLower();
        }

        /// <summary>
        /// Normalizes the slashes in a path to be <see cref="Path.AltDirectorySeparatorChar"/>
        /// </summary>
        /// <example>/manga/1\1 -> /manga/1/1</example>
        /// <returns></returns>
        public string NormalizePath()
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', Path.AltDirectorySeparatorChar)
                .Replace(@"//", Path.AltDirectorySeparatorChar + string.Empty);
        }

        public float AsFloat(float defaultValue = 0.0f)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : float.Parse(value, CultureInfo.InvariantCulture);
        }

        public double AsDouble(double defaultValue = 0.0f)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : double.Parse(value, CultureInfo.InvariantCulture);
        }

        public string TrimPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            if (!value.StartsWith(prefix)) return value;

            return value.Substring(prefix.Length);
        }

        /// <summary>
        /// Censor the input string by removing all but the first and last char.
        /// </summary>
        /// <returns></returns>
        /// <remarks>If the input is an email (contains @), the domain will remain untouched</remarks>
        public string Censor()
        {
            if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;

            var atIdx = value.IndexOf('@');
            if (atIdx == -1)
            {
                return $"{value[0]}{new string('*', value.Length - 1)}";
            }

            return value[0] + new string('*', atIdx - 1) + value[atIdx..];
        }

        /// <summary>
        /// Repeat returns a string that is equal to the original string repeat n times
        /// </summary>
        /// <param name="n">Amount of times to repeat</param>
        /// <returns></returns>
        public string Repeat(int n)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : string.Concat(Enumerable.Repeat(value, n));
        }
    }

    extension(string value)
    {
        /// <summary>
        /// Splits the string by the given separator. While cleaning out entries and removing duplicates
        /// </summary>
        /// <param name="separator"></param>
        /// <returns></returns>
        public IList<string> SplitBy(char separator)
        {
            if (string.IsNullOrEmpty(value))
            {
                return ImmutableList<string>.Empty;
            }

            return value.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .DistinctBy(s => s.ToNormalized())
                .ToList();
        }

        public IList<int> ParseIntArray()
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
        /// <returns>Byte count as long</returns>
        /// <param name="value">The input string like "1.43 GB", "4.2 KB", "512 B"</param>
        public long ParseHumanReadableBytes()
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(value));
            }


            var match = HumanReadableBytesRegex().Match(value);
            if (!match.Success)
            {
                throw new FormatException($"Invalid format: '{value}'");
            }


            var value1 = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
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

            return (long)(value1 * multiplier);
        }
    }

    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)?)\s*([KMGTPE]?B)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HumanReadableBytesRegex();
}
