using System.Collections.Generic;
using API.Services.Tasks.Scanner.Parser;

namespace API.Comparators;

#nullable enable

/// <summary>
/// Sorts chapters based on their Number. Uses natural ordering of doubles. Specials always LAST.
/// </summary>
public class ChapterSortComparerSpecialsLast : IComparer<double>
{
    /// <summary>
    /// Normal sort for 2 doubles. 0 always comes last
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int Compare(double x, double y)
    {
        if (x == Parser.DefaultChapterNumber && y == Parser.DefaultChapterNumber) return 0;
        // if x is 0, it comes second
        if (x == Parser.DefaultChapterNumber) return 1;
        // if y is 0, it comes second
        if (y == Parser.DefaultChapterNumber) return -1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparerSpecialsLast Default = new ChapterSortComparerSpecialsLast();
}

/// <summary>
/// This is a special case comparer used exclusively for sorting chapters within a single Volume for reading order.
/// <example>
/// Volume 10 has "Series - Vol 10" and "Series - Vol 10 Chapter 81". In this case, for reading order, the order is Vol 10, Vol 10 Chapter 81.
/// This is represented by Chapter 0, Chapter 81.
/// </example>
/// </summary>
public class ChapterSortComparerSpecialsFirst : IComparer<double>
{
    // TODO: Refactor this to be ChapterSortSpecialFirst
    public int Compare(double x, double y)
    {
        if (x == Parser.DefaultChapterNumber && y == Parser.DefaultChapterNumber) return 0;
        // if x is 0, it comes first
        if (x == Parser.DefaultChapterNumber) return -1;
        // if y is 0, it comes first
        if (y == Parser.DefaultChapterNumber) return 1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparerSpecialsFirst Default = new ChapterSortComparerSpecialsFirst();
}
