using System;

namespace Kavita.Common.Helpers;

public static class DayOfWeekHelper
{
    private static readonly Random Rnd = new();

    /// <summary>
    /// Returns a random DayOfWeek value.
    /// </summary>
    /// <returns>A randomly selected DayOfWeek.</returns>
    public static DayOfWeek Random()
    {
        var values = Enum.GetValues<DayOfWeek>();
        return (DayOfWeek)values.GetValue(Rnd.Next(values.Length))!;
    }
}
