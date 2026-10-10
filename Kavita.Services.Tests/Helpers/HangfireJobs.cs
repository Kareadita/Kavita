using System;
using System.Reflection;
using Hangfire;
using Hangfire.States;

namespace Kavita.Services.Tests.Helpers;

public static class HangfireJobs
{
    /// <summary>
    /// Moves an enqueued job to Processing, as a worker picking it up would
    /// </summary>
    public static void MarkProcessing(string jobId)
    {
        // ProcessingState's constructor is internal, Hangfire only creates it from a worker
        var processing = (ProcessingState) Activator.CreateInstance(typeof(ProcessingState),
            BindingFlags.Instance | BindingFlags.NonPublic, null, ["server", "worker"], null)!;
        if (!new BackgroundJobClient().ChangeState(jobId, processing, EnqueuedState.StateName))
        {
            throw new InvalidOperationException($"Job {jobId} was not in the Enqueued state");
        }
    }
}
