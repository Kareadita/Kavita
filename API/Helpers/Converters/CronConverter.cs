using System.Collections.Generic;
using Hangfire;

namespace API.Helpers.Converters
{
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

        // public static string ConvertFromCronNotation(string cronNotation)
        // {
        //     var destination = string.Empty;
        //     destination = cronNotation.ToLower() switch
        //     {
        //         "0 0 31 2 *" => "disabled",
        //         _ => destination
        //     };
        //
        //     return destination;
        // }
    }
}
