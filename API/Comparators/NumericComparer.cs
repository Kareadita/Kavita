using System.Collections;

namespace API.Comparators;

#nullable enable

public class NumericComparer : IComparer
{

    public int Compare(object? x, object? y)
    {
        if((x is string xs) && (y is string ys))
        {
            return StringLogicalComparer.Compare(xs, ys);
        }
        return -1;
    }
}
