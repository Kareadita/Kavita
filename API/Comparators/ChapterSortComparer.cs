using System.Collections.Generic;

namespace API.Comparators;

#nullable enable

/// <summary>
/// Sorts chapters based on their Number. Uses natural ordering of doubles.
/// </summary>
public class ChapterSortComparer : IComparer<double>
{
    /// <summary>
    /// Normal sort for 2 doubles. 0 always comes last
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int Compare(double x, double y)
    {
        if (x == 0.0 && y == 0.0) return 0;
        // if x is 0, it comes second
        if (x == 0.0) return 1;
        // if y is 0, it comes second
        if (y == 0.0) return -1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparer Default = new ChapterSortComparer();
}

/// <summary>
/// This is a special case comparer used exclusively for sorting chapters within a single Volume for reading order.
/// <example>
/// Volume 10 has "Series - Vol 10" and "Series - Vol 10 Chapter 81". In this case, for reading order, the order is Vol 10, Vol 10 Chapter 81.
/// This is represented by Chapter 0, Chapter 81.
/// </example>
/// </summary>
public class ChapterSortComparerZeroFirst : IComparer<double>
{
    public int Compare(double x, double y)
    {
        if (x == 0.0 && y == 0.0) return 0;
        // if x is 0, it comes first
        if (x == 0.0) return -1;
        // if y is 0, it comes first
        if (y == 0.0) return 1;

        return x.CompareTo(y);
    }

    public static readonly ChapterSortComparerZeroFirst Default = new ChapterSortComparerZeroFirst();
}

public class SortComparerZeroLast : IComparer<double>
{
    public int Compare(double x, double y)
    {
        if (x == 0.0 && y == 0.0) return 0;
        // if x is 0, it comes last
        if (x == 0.0) return 1;
        // if y is 0, it comes last
        if (y == 0.0) return -1;

        return x.CompareTo(y);
    }
    public static readonly SortComparerZeroLast Default = new SortComparerZeroLast();
}
