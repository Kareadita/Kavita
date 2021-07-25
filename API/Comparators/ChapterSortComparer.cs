using System.Collections.Generic;

namespace API.Comparators
{
    public class ChapterSortComparer : IComparer<double>
    {
        public int Compare(double x, double y)
        {
            if (x == 0.0 && y == 0.0) return 0;
            // if x is 0, it comes second
            if (x == 0.0) return 1;
            // if y is 0, it comes second
            if (y == 0.0) return -1;

            return x.CompareTo(y);
        }
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
    }
}
