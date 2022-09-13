using System.Collections.Generic;
using Hangfire;

namespace API.Helpers.Converters;

public static class CronConverter
{
    public static readonly IEnumerable<string> Options = new []
    {
        "disabled",
        "daily",
        "weekly",
    };
    public static string ConvertToCronNotation(string source)
    {
        var destination = string.Empty;
        destination = source.ToLower() switch
        {
            "daily" => Cron.Daily(),
            "weekly" => Cron.Weekly(),
            "disabled" => Cron.Never(),
            "" => Cron.Never(),
            _ => destination
        };

        return destination;
    }
}
