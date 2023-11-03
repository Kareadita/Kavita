using System;

namespace API.Extensions;
#nullable enable

public static class DateTimeExtensions
{
    /// <summary>
    /// <para>Truncates a DateTime to a specified resolution.</para>
    /// <para>A convenient source for resolution is TimeSpan.TicksPerXXXX constants.</para>
    /// </summary>
    /// <param name="date">The DateTime object to truncate</param>
    /// <param name="resolution">e.g. to round to nearest second, TimeSpan.TicksPerSecond</param>
    /// <returns>Truncated DateTime</returns>
    public static DateTime Truncate(this DateTime date, long resolution)
    {
        return new DateTime(date.Ticks - (date.Ticks % resolution), date.Kind);
    }

    public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
    {
        int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
        return dt.AddDays(-1 * diff).Date;
    }
}
