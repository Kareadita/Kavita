using System.Text.Json.Serialization;

namespace FsProbe;

public sealed class EnvironmentInfo
{
    public string ProbeVersion { get; set; } = "";
    public string Mode { get; set; } = "";
    public string? Label { get; set; }
    public string StartedUtc { get; set; } = "";
    public string Os { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string Rid { get; set; } = "";
    public bool InContainer { get; set; }
    public string Path { get; set; } = "";
    public string? MountPoint { get; set; }
    public string? MountSource { get; set; }
    public string? FileSystem { get; set; }
    public string? MountOptions { get; set; }
    public string? DriveType { get; set; }
    public bool ChangeTimeSupported { get; set; }
}

public sealed class MethodTiming
{
    public string Method { get; set; } = "";
    public int Run { get; set; }
    public double Milliseconds { get; set; }
    public string? ResultUtc { get; set; }
}

public sealed class PassiveReport
{
    public EnvironmentInfo Environment { get; set; } = new();
    public int Files { get; set; }
    public int Folders { get; set; }
    public List<MethodTiming> Timings { get; set; } = [];
    public bool RootResultsMatchTruncated { get; set; }
    public ParityResult Parity { get; set; } = new();
    public ChangeTimeResult? ChangeTime { get; set; }
    public int FutureMtime { get; set; }
    public int MtimeBefore1990 { get; set; }
}

public sealed class ParityResult
{
    public int Compared { get; set; }
    public int MismatchRaw { get; set; }
    public int MismatchTruncated { get; set; }
    public int FoldersAffected { get; set; }
    public double MaxDeltaMs { get; set; }
    public List<ParityExample> Examples { get; set; } = [];
}

public sealed class ParityExample
{
    public string Path { get; set; } = "";
    public string PerFileUtc { get; set; } = "";
    public string ListingUtc { get; set; } = "";
    public double DeltaMs { get; set; }
}

public sealed class ChangeTimeResult
{
    public int Read { get; set; }
    public int Failed { get; set; }
    public int CtimeAfterMtime { get; set; }
    public List<ChangeTimeExample> Examples { get; set; } = [];
}

public sealed class ChangeTimeExample
{
    public string Path { get; set; } = "";
    public string MtimeUtc { get; set; } = "";
    public string CtimeUtc { get; set; } = "";
}

public sealed class ActiveReport
{
    public EnvironmentInfo Environment { get; set; } = new();
    public string ProbeFolder { get; set; } = "";
    public double? ClockSkewMs { get; set; }
    public List<int> Delays { get; set; } = [];
    public List<ScenarioResult> Scenarios { get; set; } = [];
}

public sealed class ScenarioResult
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Error { get; set; }
    public string BaselineUtc { get; set; } = "";
    public SignalValues? Baseline { get; set; }
    public List<ScenarioSnapshot> Snapshots { get; set; } = [];
}

public sealed class ScenarioSnapshot
{
    public int DelaySeconds { get; set; }
    public string TakenUtc { get; set; } = "";
    public Verdicts Caught { get; set; } = new();
    public bool CandidateMatchesCurrent { get; set; }
    public SignalValues Values { get; set; } = new();
}

/// <summary>
/// Which way of checking would have noticed the change
/// </summary>
public sealed class Verdicts
{
    public bool FolderOnly { get; set; }
    public bool Current { get; set; }
    public bool Candidate { get; set; }
    public bool NamesAndSizes { get; set; }
    public bool? ChangeTime { get; set; }
}

public sealed class SignalValues
{
    public string MaxFolderOwnUtc { get; set; } = "";
    public string CurrentUtc { get; set; } = "";
    public string CandidateUtc { get; set; } = "";
    public string? MaxChangeTimeUtc { get; set; }
    public string NamesAndSizes { get; set; } = "";
    public TargetFile? Target { get; set; }
}

public sealed class TargetFile
{
    public bool Exists { get; set; }
    public string? PerFileUtc { get; set; }
    public string? ListingUtc { get; set; }
    public long? Size { get; set; }
    public string? ChangeTimeUtc { get; set; }
}

public sealed class Snapshot
{
    public EnvironmentInfo Environment { get; set; } = new();
    public long MarkedTicks { get; set; }
    public List<SnapshotEntry> Entries { get; set; } = [];
}

public sealed class SnapshotEntry
{
    public string Path { get; set; } = "";
    public bool IsFolder { get; set; }
    public long PerFileTicks { get; set; }
    public long ListingTicks { get; set; }
    public long Size { get; set; }
    public long? ChangeTicks { get; set; }
}

public sealed class CheckReport
{
    public EnvironmentInfo Environment { get; set; } = new();
    public string MarkedUtc { get; set; } = "";
    public string? MarkedEnvironmentPath { get; set; }
    public int Changes { get; set; }
    public CheckSummary Caught { get; set; } = new();
    public List<CheckChange> Details { get; set; } = [];
}

public sealed class CheckSummary
{
    public int FolderOnly { get; set; }
    public int Current { get; set; }
    public int Candidate { get; set; }
    public int NamesAndSizes { get; set; }
    public int ChangeTime { get; set; }
}

public sealed class CheckChange
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public List<string> FieldsChanged { get; set; } = [];
    public Verdicts Caught { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PassiveReport))]
[JsonSerializable(typeof(ActiveReport))]
[JsonSerializable(typeof(Snapshot))]
[JsonSerializable(typeof(CheckReport))]
public partial class ProbeJsonContext : JsonSerializerContext;
