using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FsProbe;

public static class EnvironmentReader
{
    public const string ProbeVersion = "1";

    public static EnvironmentInfo Read(string mode, string path, string? label)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new EnvironmentInfo
        {
            ProbeVersion = ProbeVersion,
            Mode = mode,
            Label = label,
            StartedUtc = DateTime.UtcNow.ToString("O"),
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Runtime = RuntimeInformation.FrameworkDescription,
            Rid = RuntimeInformation.RuntimeIdentifier,
            InContainer = IsInContainer(),
            Path = fullPath,
            ChangeTimeSupported = ChangeTime.Supported,
        };

        if (OperatingSystem.IsLinux()) ReadLinuxMount(fullPath, info);
        else if (OperatingSystem.IsWindows()) ReadWindowsDrive(fullPath, info);

        return info;
    }

    private static bool IsInContainer()
    {
        if (File.Exists("/.dockerenv") || File.Exists("/run/.containerenv")) return true;
        try
        {
            var cgroup = File.ReadAllText("/proc/1/cgroup");
            return cgroup.Contains("docker") || cgroup.Contains("kubepods") || cgroup.Contains("containerd");
        }
        catch
        {
            return false;
        }
    }

    private static void ReadLinuxMount(string fullPath, EnvironmentInfo info)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines("/proc/mounts");
        }
        catch
        {
            return;
        }

        var resolved = ResolveLinks(fullPath);
        var bestLength = -1;
        foreach (var line in lines)
        {
            var parts = line.Split(' ');
            if (parts.Length < 4) continue;

            var mountPoint = Unescape(parts[1]);
            var inside = resolved == mountPoint
                         || mountPoint == "/"
                         || resolved.StartsWith(mountPoint.TrimEnd('/') + "/", StringComparison.Ordinal);
            if (!inside || mountPoint.Length <= bestLength) continue;

            bestLength = mountPoint.Length;
            info.MountPoint = mountPoint;
            info.MountSource = Unescape(parts[0]);
            info.FileSystem = parts[2];
            info.MountOptions = parts[3];
        }
    }

    // /proc/mounts writes space, tab, newline and backslash as octal escapes
    private static string Unescape(string value) =>
        Regex.Replace(value, @"\\([0-7]{3})", m => ((char) Convert.ToInt32(m.Groups[1].Value, 8)).ToString());

    private static string ResolveLinks(string fullPath)
    {
        try
        {
            var target = new DirectoryInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? fullPath;
        }
        catch
        {
            return fullPath;
        }
    }

    private static void ReadWindowsDrive(string fullPath, EnvironmentInfo info)
    {
        var root = Path.GetPathRoot(fullPath);
        info.MountPoint = root;
        if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\"))
        {
            info.DriveType = "Network (UNC)";
            return;
        }

        try
        {
            var drive = new DriveInfo(root);
            info.DriveType = drive.DriveType.ToString();
            info.FileSystem = drive.DriveFormat;
        }
        catch (Exception ex)
        {
            info.DriveType = $"unknown ({ex.GetType().Name})";
        }
    }
}
