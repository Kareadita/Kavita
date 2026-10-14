using System;
using System.Globalization;

namespace Kavita.Models.Parser;

/// <summary>
/// A file as the database last saw it
/// </summary>
/// <param name="Path">Normalized full path</param>
/// <param name="LastWriteTimeUtc">Null for rows not read since the column was added</param>
/// <param name="SortOrderDependsOnSiblings">
/// The file's chapter is a special or its range is not a plain number (<c>15.UH</c>, <c>4-6</c>). Its sort order is worked
/// out from the other files in its volume, so a changed folder holding one is parsed in full, see <see cref="IsSortedBySiblings"/>
/// </param>
public sealed record KnownFile(int Id, string Path, long Bytes, DateTime? LastWriteTimeUtc, bool SortOrderDependsOnSiblings = false)
{
    /// <summary>
    /// Specials are numbered by their position (<c>SortOrder</c>) among the volume's files, and a non-plain range takes +0.1 from a sibling
    /// with the same base number, so neither can be sorted from its own file alone
    /// </summary>
    /// <param name="chapterRange">The chapter's range, or a parsed file's chapters</param>
    public static bool IsSortedBySiblings(string chapterRange, bool isSpecial)
    {
        return isSpecial || !float.TryParse(chapterRange, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
