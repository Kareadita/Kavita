using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace API.Helpers;

public static class TaskHelper
{


    /// <summary>
    /// Wrap a simple action in a retry mechanism. Allowing up to <see cref="maxAttempts"/> attempts, with at least
    /// <see cref="baseDelay"/> between each attempt
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="action"></param>
    /// <param name="maxAttempts"></param>
    /// <param name="baseDelay">Base delay in ms</param>
    public static async Task WithRetry(ILogger logger, Func<Task> action, int maxAttempts, int baseDelay)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "An exception occurred running task {Attempt}/{MaxAttempts}", attempt + 1,maxAttempts);

                var delay = baseDelay * 2 * attempt;
                var jitter = Random.Shared.Next(0, delay / 4);
                await Task.Delay(delay + jitter);
            }
        }

        logger.LogError("Task failed to execute after {Attempts} attempts", maxAttempts);
    }

}
