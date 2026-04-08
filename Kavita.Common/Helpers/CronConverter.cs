using System.Collections.Generic;
using Hangfire;

namespace Kavita.Common.Helpers;

public static class CronConverter
{
    public static readonly IEnumerable<string> Options =
    [
        "disabled",
        "daily",
        "weekly"
    ];

    /// <summary>
    /// Converts to Cron Notation
    /// </summary>
    /// <param name="source">Defaults to daily</param>
    /// <returns></returns>
    public static string ConvertToCronNotation(string? source)
    {
        if (string.IsNullOrEmpty(source)) return Cron.Daily();
        return source.ToLower() switch
        {
            "daily" => Cron.Daily(),
            "weekly" => Cron.Weekly(),
            "disabled" => Cron.Never(),
            "" => Cron.Never(),
            _ => source
        };
    }
}
