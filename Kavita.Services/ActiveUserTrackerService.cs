using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

/// <summary>
/// Responsible to track userId via <see cref="UpdateUserAsActiveMiddleware"/>. Flushes the queue every 5 mins.
/// </summary>
public sealed class ActiveUserTrackerService( IServiceScopeFactory serviceScopeFactory, ILogger<ActiveUserTrackerService> logger)
    : IActiveUserTrackerService, IHostedService
{
    private readonly RateLimiter _rateLimiter = new(1, TimeSpan.FromMinutes(5), refillBetween: false);
    private readonly ConcurrentDictionary<int, DateTime> _pendingUpdates = new();

    /// <summary>
    /// Enqueue that the userId was seen
    /// </summary>
    /// <param name="userId"></param>
    public void RecordActive(int userId)
    {
        if (_rateLimiter.TryAcquire(userId.ToString()))
        {
            _pendingUpdates[userId] = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Flush the queue of userId's to the DB.
    /// </summary>
    /// <remarks>Expected to be run by Hangfire, not invoked manually</remarks>
    /// <param name="ct"></param>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_pendingUpdates.IsEmpty) return;

        var userIds = _pendingUpdates.Keys.ToList();

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();

            await context.Users
                .Where(u => userIds.Contains(u.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.LastActiveUtc, DateTime.UtcNow)
                    .SetProperty(u => u.LastActive, DateTime.Now), ct);

            foreach (var key in userIds)
            {
                _pendingUpdates.TryRemove(key, out _);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk update LastActive for {Count} users", userIds.Count);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Flushing pending LastActive updates before shutdown");
        await FlushAsync(cancellationToken);
    }
}
