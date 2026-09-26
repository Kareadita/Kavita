using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kavita.Services.Helpers;

/// <summary>
/// Responsible for parsing book titles "The man on the street" and removing the prefix -> "man on the street".
/// </summary>
/// <remarks>This code is performance sensitive</remarks>
public static class BookSortTitlePrefixHelper
{
    private static readonly Dictionary<string, byte> PrefixLookup;
    private static readonly Dictionary<char, List<string>> PrefixesByFirstChar;
    private static readonly Dictionary<string, Dictionary<string, byte>> PrefixLookupByLanuage;
    private static readonly Dictionary<string, Dictionary<char, List<string>>> PrefixesByFirstCharByLanuage;

    static BookSortTitlePrefixHelper()
    {
        var prefixesByLanguage = new Dictionary<string, List<string>>
        {
            // English
            ["en"] = ["the", "a", "an"],
            // Spanish
            ["es"] = ["el", "la", "los", "las", "un", "una", "unos", "unas"],
            // French
            ["fr"] = ["le", "la", "les", "un", "une", "des"],
            // German
            ["de"] = ["der", "die", "das", "den", "dem", "ein", "eine", "einen", "einer"],
            // Italian
            ["it"] = ["il", "lo", "la", "gli", "le", "un", "uno", "una"],
            // Portuguese
            ["pt"] = ["o", "a", "os", "as", "um", "uma", "uns", "umas"],
            // Russian (transliterated common ones)
            ["ru"] = ["в", "на", "с", "к", "от", "для",]
        };

        var totalPrefixes = prefixesByLanguage.Values.Select(v => v.Count).Sum();

        // Build lookup structures
        PrefixLookup = new Dictionary<string, byte>(totalPrefixes, StringComparer.OrdinalIgnoreCase);
        PrefixesByFirstChar = new Dictionary<char, List<string>>();
        PrefixLookupByLanuage = new Dictionary<string, Dictionary<string, byte>>(prefixesByLanguage.Count);
        PrefixesByFirstCharByLanuage = new Dictionary<string, Dictionary<char, List<string>>>();

        foreach (var (language, prefixes) in prefixesByLanguage)
        {
            PrefixLookupByLanuage[language] = new Dictionary<string, byte>(prefixes.Count, StringComparer.OrdinalIgnoreCase);
            PrefixesByFirstCharByLanuage[language] = new Dictionary<char, List<string>>();

            foreach (var prefix in prefixes)
            {
                PrefixLookup[prefix] = 1;
                PrefixLookupByLanuage[language][prefix] = 1;

                var firstChar = char.ToLowerInvariant(prefix[0]);
                if (!PrefixesByFirstCharByLanuage[language].TryGetValue(firstChar, out var list))
                {
                    list = [];
                    PrefixesByFirstCharByLanuage[language][firstChar] = list;
                }
                if (!PrefixesByFirstChar.TryGetValue(firstChar, out var list2))
                {
                    list2 = [];
                    PrefixesByFirstChar[firstChar] = list2;
                }

                list.Add(prefix);
                list2.Add(prefix);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> GetSortTitle(ReadOnlySpan<char> title, Dictionary<string, byte> lookup, Dictionary<char, List<string>> byFirstChar)
    {
        if (title.IsEmpty) return title;

        // Fast detection of script type by first character
        var firstChar = title[0];

        // CJK Unicode ranges - no processing needed for most cases
        if ((firstChar >= 0x4E00 && firstChar <= 0x9FFF) ||   // CJK Unified
            (firstChar >= 0x3040 && firstChar <= 0x309F) ||   // Hiragana
            (firstChar >= 0x30A0 && firstChar <= 0x30FF))     // Katakana
        {
            return title;
        }

        var firstSpaceIndex = title.IndexOf(' ');
        if (firstSpaceIndex <= 0) return title;

        var potentialPrefix = title.Slice(0, firstSpaceIndex);

        // Fast path: check if first character could match any prefix
        firstChar = char.ToLowerInvariant(potentialPrefix[0]);
        if (!byFirstChar.ContainsKey(firstChar))
            return title;

        // Only do the expensive lookup if first character matches
        if (lookup.ContainsKey(potentialPrefix.ToString()))
        {
            var remainder = title.Slice(firstSpaceIndex + 1);
            return remainder.IsEmpty ? title : remainder;
        }

        return title;
    }

    /// <summary>
    /// Removes the sort prefix
    /// </summary>
    /// <param name="title"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static string GetSortTitle(string title, string language = "")
    {
        language = NormalizeLanguage(language);

        var lookup = PrefixLookupByLanuage.GetValueOrDefault(language, PrefixLookup);
        var byFirstChar = PrefixesByFirstCharByLanuage.GetValueOrDefault(language, PrefixesByFirstChar);

        var result = GetSortTitle(title.AsSpan(), lookup, byFirstChar);

        return result.ToString();
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrEmpty(language)) return string.Empty;
        try
        {
            return CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return string.Empty;
        }
    }
}
