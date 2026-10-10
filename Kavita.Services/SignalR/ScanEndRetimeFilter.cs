using System;
using Hangfire;
using Hangfire.Server;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.SignalR;

/// <summary>
/// When a scanner job ends, pulls the delayed scan asked for first forward. See <see cref="ITaskScheduler.RetimeDelayedScans"/>
/// </summary>
public class ScanEndRetimeFilter(ILogger<ScanEndRetimeFilter> logger) : IServerFilter
{
    private static readonly TimeSpan RetimeDelay = TimeSpan.FromSeconds(5);

    public void OnPerforming(PerformingContext context)
    {
    }

    public void OnPerformed(PerformedContext context)
    {
        var job = context.BackgroundJob.Job;
        if (!job.Type.IsAssignableTo(typeof(IScannerService))) return;

        try
        {
            // The ending job is still Processing here, so the retime runs a moment later when it can tell whether the scanner is free
            BackgroundJob.Schedule<ITaskScheduler>(t => t.RetimeDelayedScans(), RetimeDelay);
        }
        catch (Exception ex)
        {
            // An exception thrown here would mark the scan job itself as failed
            logger.LogWarning(ex, "Could not retime delayed scans after {Method} ended", job.Method.Name);
        }
    }
}
