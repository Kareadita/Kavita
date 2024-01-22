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
    public static string ConvertToCronNotation(string source)
    {
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
