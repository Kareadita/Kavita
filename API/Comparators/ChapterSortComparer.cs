using System.Collections.Generic;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;

namespace API.Comparators;

#nullable enable

/// <summary>
/// Sorts chapters based on their Number. Uses natural ordering of doubles. Specials always LAST.
/// </summary>
public class ChapterSortComparerDefaultLast : IComparer<float>
{
    /// <summary>
    /// Normal sort for 2 doubles. DefaultChapterNumber always comes last
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int Compare(float x, float y)
    {
        if (x.Is(Parser.DefaultChapterNumber) && y.Is(Parser.DefaultChapterNumber)) return 0;
        // if x is 0, it comes second
        if (x.Is(Parser.DefaultChapterNumber)) return 1;
        // if y is 0, it comes second
        if (y.Is(Parser.DefaultChapterNumber)) return -1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparerDefaultLast Default = new ChapterSortComparerDefaultLast();
}

/// <summary>
/// This is a special case comparer used exclusively for sorting chapters within a single Volume for reading order.
/// <example>
/// Volume 10 has "Series - Vol 10" and "Series - Vol 10 Chapter 81". In this case, for reading order, the order is Vol 10, Vol 10 Chapter 81.
/// This is represented by Chapter 0, Chapter 81.
/// </example>
/// </summary>
public class ChapterSortComparerDefaultFirst : IComparer<float>
{
    public int Compare(float x, float y)
    {
        if (x.Is(Parser.DefaultChapterNumber) && y.Is(Parser.DefaultChapterNumber)) return 0;
        // if x is 0, it comes first
        if (x.Is(Parser.DefaultChapterNumber)) return -1;
        // if y is 0, it comes first
        if (y.Is(Parser.DefaultChapterNumber)) return 1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparerDefaultFirst Default = new ChapterSortComparerDefaultFirst();
}

/// <summary>
/// Sorts chapters based on their Number. Uses natural ordering of doubles. Specials always LAST.
/// </summary>
public class ChapterSortComparerSpecialsLast : IComparer<float>
{
    /// <summary>
    /// Normal sort for 2 doubles. DefaultSpecialNumber always comes last
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int Compare(float x, float y)
    {
        if (x.Is(Parser.SpecialVolumeNumber) && y.Is(Parser.SpecialVolumeNumber)) return 0;
        // if x is 0, it comes second
        if (x.Is(Parser.SpecialVolumeNumber)) return 1;
        // if y is 0, it comes second
        if (y.Is(Parser.SpecialVolumeNumber)) return -1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparerSpecialsLast Default = new ChapterSortComparerSpecialsLast();
}
