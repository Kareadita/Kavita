using System;
using System.Globalization;
using System.IO;

namespace Kavita.Services.Helpers.Kobo;

public static class KoboEpubHelper
{
    private const string SectionPrefix = "section";

    /// <summary>
    /// Returns the 0-based Kavita page for a Kobo section path. A split half maps to its source page
    /// </summary>
    /// <remarks>Sections are 1-based indexed</remarks>
    /// <example>TryGetPage("EPUB/section13-2.html") -> true, 12</example>
    public static bool TryGetPage(string? path, out int page)
    {
        page = 0;
        if (string.IsNullOrEmpty(path)) return false;

        var name = Path.GetFileNameWithoutExtension(path).AsSpan();
        if (!name.StartsWith(SectionPrefix, StringComparison.Ordinal)) return false;

        var number = name[SectionPrefix.Length..];
        var dash = number.IndexOf('-');
        if (dash >= 0)
        {
            if (!TryParsePositive(number[(dash + 1)..], out _)) return false;
            number = number[..dash];
        }

        if (!TryParsePositive(number, out var section)) return false;

        page = section - 1;
        return true;
    }

    /// <summary>
    /// Returns the section path for a 0-based Kavita page. Part 2 and up are the halves of a split page
    /// </summary>
    /// <example>GetPath(12) -> "EPUB/section13.html", GetPath(12, 2) -> "EPUB/section13-2.html"</example>
    public static string GetPath(int page, int part = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(part, 1);

        var suffix = part == 1 ? string.Empty : $"-{part}";
        return $"EPUB/{SectionPrefix}{page + 1}{suffix}.html";
    }

    private static bool TryParsePositive(ReadOnlySpan<char> value, out int number)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
    }
}
