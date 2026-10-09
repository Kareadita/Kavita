using System;
using System.Collections.Immutable;
using Hangfire.Server;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.SignalR;

/// <summary>
/// When a scan job ends, pulls the delayed scan asked for first forward. See <see cref="ITaskScheduler.RetimeDelayedScans"/>
/// </summary>
public class ScanEndRetimeFilter(IServiceScopeFactory scopeFactory, ILogger<ScanEndRetimeFilter> logger) : IServerFilter
{
    private static readonly ImmutableArray<string> ScanMethods =
        [
            nameof(IScannerService.ScanLibrary), nameof(IScannerService.ScanLibraries),
            nameof(IScannerService.ScanSeries), nameof(IScannerService.ScanFolder),
        ];

    public void OnPerforming(PerformingContext context)
    {
    }

    public void OnPerformed(PerformedContext context)
    {
        var job = context.BackgroundJob.Job;
        // TaskScheduler has wrappers with the same method names that end as soon as they enqueue the real scan
        if (!job.Type.IsAssignableTo(typeof(IScannerService)) || !ScanMethods.Contains(job.Method.Name)) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITaskScheduler>().RetimeDelayedScans().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // An exception thrown here would mark the scan job itself as failed
            logger.LogWarning(ex, "Could not retime delayed scans after {Method} ended", job.Method.Name);
        }
    }
}
