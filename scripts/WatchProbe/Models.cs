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
public partial class WatchJsonContext : JsonSerializerContext;
