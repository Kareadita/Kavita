using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Database.Extensions;

public static class LoggerExtensions
{

    public static void LogDbUpdateConcurrencyException<T>(this ILogger<T> logger, DbUpdateConcurrencyException ex)
    {
        var debugInfo = ex.Entries.Select(e => e.DebugView.ShortView);

        logger.LogError(ex, "Concurrency conflict. Debug info: {DebugInfo} ", string.Join("\n", debugInfo));
    }

}
