using System.Threading;
using Kavita.Common.EnvironmentInfo;

namespace Kavita.Services.SignalR;

/// <summary>
/// The Hangfire job the current code is running under, so every message it sends can be tied to that job run
/// </summary>
public static class JobCorrelation
{
    private static readonly string BootPrefix = BuildInfo.BootId.ToString("N")[..8];
    private static readonly AsyncLocal<string?> JobId = new();

    public static string? CurrentJobId
    {
        get => JobId.Value;
        set => JobId.Value = value;
    }

    /// <summary>
    /// <c>{boot}.{jobId}</c>, e.g. <c>7f3a91c2.184</c>. Null outside a job
    /// </summary>
    public static string? CurrentCorrelationId => JobId.Value is { } jobId ? $"{BootPrefix}.{jobId}" : null;
}
