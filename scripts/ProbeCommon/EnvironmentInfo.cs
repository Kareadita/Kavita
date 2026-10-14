namespace ProbeCommon;

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
