using System;

namespace API.Extensions;

public static class FloatExtensions
{
    private const float Tolerance = 0.001f;

    /// <summary>
    /// Used to compare 2 floats together
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool Is(this float a, float? b)
    {
        if (!b.HasValue) return false;
        return Math.Abs((float) (a - b)) < Tolerance;
    }

    public static bool IsNot(this float a, float? b)
    {
        if (!b.HasValue) return false;
        return Math.Abs((float) (a - b)) > Tolerance;
    }
}
