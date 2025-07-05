using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace API.Helpers;

/// <summary>
/// Responsible for parsing book titles "The man on the street" and removing the prefix -> "man on the street".
/// </summary>
/// <remarks>This code is performance sensitive</remarks>
public static class BookSortTitlePrefixHelper
{
    private static readonly Dictionary<string, byte> PrefixLookup;
    private static readonly Dictionary<char, List<string>> PrefixesByFirstChar;

    static BookSortTitlePrefixHelper()
    {
        var prefixes = new[]
        {
            // English
            "the", "a", "an",
            // Spanish
            "el", "la", "los", "las", "un", "una", "unos", "unas",
            // French
            "le", "la", "les", "un", "une", "des",
            // German
            "der", "die", "das", "den", "dem", "ein", "eine", "einen", "einer",
            // Italian
            "il", "lo", "la", "gli", "le", "un", "uno", "una",
            // Portuguese
            "o", "a", "os", "as", "um", "uma", "uns", "umas",
            // Russian (transliterated common ones)
            "в", "на", "с", "к", "от", "для",
        };

        // Build lookup structures
        PrefixLookup = new Dictionary<string, byte>(prefixes.Length, StringComparer.OrdinalIgnoreCase);
        PrefixesByFirstChar = new Dictionary<char, List<string>>();

        foreach (var prefix in prefixes)
        {
            PrefixLookup[prefix] = 1;

            var firstChar = char.ToLowerInvariant(prefix[0]);
            if (!PrefixesByFirstChar.TryGetValue(firstChar, out var list))
            {
                list = [];
                PrefixesByFirstChar[firstChar] = list;
            }
            list.Add(prefix);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> GetSortTitle(ReadOnlySpan<char> title)
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
        if (!PrefixesByFirstChar.ContainsKey(firstChar))
            return title;

        // Only do the expensive lookup if first character matches
        if (PrefixLookup.ContainsKey(potentialPrefix.ToString()))
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
    /// <returns></returns>
    public static string GetSortTitle(string title)
    {
        var result = GetSortTitle(title.AsSpan());

        return result.ToString();
    }
}
