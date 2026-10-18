using System.Text.Json.Serialization;

namespace WatchProbe;

public sealed class WatchReport
{
    public EnvironmentInfo Environment { get; set; } = new();
    public InotifyLimits? Inotify { get; set; }
    public int IntervalSeconds { get; set; }
    public double PlannedMinutes { get; set; }
    public string EndReason { get; set; } = "";
    public double RanSeconds { get; set; }
    public TreeStats Tree { get; set; } = new();
    public List<SourceStatus> Sources { get; set; } = [];
    public ControlResult? Control { get; set; }
    public PollerStats Poller { get; set; } = new();
    public List<ChangeMatch> Changes { get; set; } = [];
    public int EventsDropped { get; set; }
    public List<WatchEvent> Events { get; set; } = [];
}

public sealed class InotifyLimits
{
    public long? MaxUserWatches { get; set; }
    public long? MaxUserInstances { get; set; }
    public long? MaxQueuedEvents { get; set; }

    public static InotifyLimits? Read()
    {
        if (!OperatingSystem.IsLinux()) return null;
        return new InotifyLimits
        {
            MaxUserWatches = ReadLong("max_user_watches"),
            MaxUserInstances = ReadLong("max_user_instances"),
            MaxQueuedEvents = ReadLong("max_queued_events"),
        };
    }

    private static long? ReadLong(string name)
    {
        try
        {
            return long.Parse(File.ReadAllText($"/proc/sys/fs/inotify/{name}").Trim());
        }
        catch
        {
            return null;
        }
    }
}

public sealed class TreeStats
{
    public int Files { get; set; }
    public int Folders { get; set; }
    public double ListingMs { get; set; }
}

public sealed class SourceStatus
{
    public string Name { get; set; } = "";
    public bool Started { get; set; }
    public string? Error { get; set; }
    public int Events { get; set; }
}

public sealed class ControlResult
{
    public string File { get; set; } = "";
    public string? Error { get; set; }
    public List<ControlStep> Steps { get; set; } = [];
}

/// <summary>
/// Seconds from the probe's own write or delete until each source reported it, null when it never did
/// </summary>
public sealed class ControlStep
{
    public string Action { get; set; } = "";
    public double AtSeconds { get; set; }
    public double? Watcher { get; set; }
    public double? Provider { get; set; }
    public double? Poller { get; set; }
}

public sealed class PollerStats
{
    public int Polls { get; set; }
    public int Errors { get; set; }
    public double? MinMs { get; set; }
    public double? MedianMs { get; set; }
    public double? MaxMs { get; set; }
}

/// <summary>
/// A change the poller found, and whether the other sources saw it around the same time
/// </summary>
public sealed class ChangeMatch
{
    public string Path { get; set; } = "";
    public bool IsFolder { get; set; }
    public string Kind { get; set; } = "";
    public string? Detail { get; set; }
    public double PreviousPollSeconds { get; set; }
    public double DetectedSeconds { get; set; }
    public double? WatcherSeconds { get; set; }
    public bool ProviderFired { get; set; }
    /// <summary>
    /// When the poller saw the parent folder's time move, in this poll or the next: a poller reading only folder times
    /// would have caught the change then. Null when it did not move, or for a folder's own time change
    /// </summary>
    public double? ParentFolderSeconds { get; set; }
}

public sealed class CostReport
{
    public EnvironmentInfo Environment { get; set; } = new();
    public int Folders { get; set; }
    public int? Files { get; set; }
    public int GapSeconds { get; set; }
    public List<CostTiming> Timings { get; set; } = [];
}

public sealed class CostTiming
{
    public string Method { get; set; } = "";
    public int Run { get; set; }
    public double Milliseconds { get; set; }
    public int Entries { get; set; }
    public int Errors { get; set; }
}

public sealed class CopyReport
{
    public EnvironmentInfo Environment { get; set; } = new();
    public string Source { get; set; } = "";
    public long SourceBytes { get; set; }
    public string SourceLastWriteUtc { get; set; } = "";
    public List<CopyRun> Runs { get; set; } = [];
}

/// <summary>
/// One way of copying the source into the watched folder, and what each watcher and the sampler saw while it ran
/// </summary>
public sealed class CopyRun
{
    public string Method { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Error { get; set; }
    public double CopySeconds { get; set; }
    /// <summary>
    /// Seconds after the copy started that the target's creation time reads, negative when it is older than the copy
    /// </summary>
    public double? CreationOffsetSeconds { get; set; }
    public bool? LastWriteKeptFromSource { get; set; }
    /// <summary>
    /// Whether an open with FileShare.Read failed with a sharing violation while the copy ran, and after it
    /// </summary>
    public bool InUseDuringCopy { get; set; }
    public bool InUseAfterCopy { get; set; }
    public double? LastInUseSeconds { get; set; }
    /// <summary>
    /// When a sample first found a valid zip end record, and whether one was found while the copy still ran: a false "complete"
    /// </summary>
    public double? ZipEndFirstSeconds { get; set; }
    public bool ZipEndDuringCopy { get; set; }
    public bool? ZipEndAfterCopy { get; set; }
    public List<CopyWatcherStats> Watchers { get; set; } = [];
    public List<CopySample> Samples { get; set; } = [];
    public List<CopyEvent> Events { get; set; } = [];
}

/// <summary>
/// Events on the target only. Middle counts the ones more than 1 s after the copy started and before it ended
/// </summary>
public sealed class CopyWatcherStats
{
    public string Name { get; set; } = "";
    public int Events { get; set; }
    public int Middle { get; set; }
    public double? LongestGapSeconds { get; set; }
}

/// <summary>
/// The target's state, written only when it differs from the previous sample
/// </summary>
public sealed class CopySample
{
    public double Seconds { get; set; }
    public bool Exists { get; set; }
    public long Bytes { get; set; }
    public string? CreationUtc { get; set; }
    public string? LastWriteUtc { get; set; }
    public bool InUse { get; set; }
    public string? OpenError { get; set; }
    public bool? ZipEnd { get; set; }
    public string? ZipEndError { get; set; }
}

public sealed class CopyEvent
{
    public string Watcher { get; set; } = "";
    public string Kind { get; set; } = "";
    public double Seconds { get; set; }
    public string Path { get; set; } = "";
}

public sealed class WatchEvent
{
    public string Source { get; set; } = "";
    public string Kind { get; set; } = "";
    public double Seconds { get; set; }
    public string AtUtc { get; set; } = "";
    public string? Path { get; set; }
    public string? OldPath { get; set; }
    public string? Detail { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WatchReport))]
[JsonSerializable(typeof(CostReport))]
[JsonSerializable(typeof(CopyReport))]
public partial class WatchJsonContext : JsonSerializerContext;
