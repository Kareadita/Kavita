using System;
using Cronos;

namespace Kavita.Common.Helpers;

public static class CronHelper
{
    public static bool IsValidCron(string cronExpression)
    {
        // NOTE: This must match Hangfire's underlying cron system. Hangfire is unique
        try
        {
            CronExpression.Parse(cronExpression);
            return true;
        }
        catch (Exception ex)
        {
            /* Swallow */
            return false;
        }
    }
}
