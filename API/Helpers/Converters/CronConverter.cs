using System.Collections.Generic;
using Hangfire;

namespace API.Helpers.Converters;
#nullable enable

public static class CronConverter
{
    public static readonly IEnumerable<string> Options = new []
    {
        "disabled",
        "daily",
        "weekly",
    };
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
