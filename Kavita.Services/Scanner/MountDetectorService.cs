using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Kavita.API.Services.Scanner;
using Kavita.Models.Scanner;

namespace Kavita.Services.Scanner;

public partial class MountDetectorService : IMountDetectorService
{
    private const string ProcMounts = "/proc/mounts";
    private const string MountCommand = "/sbin/mount";
    private static readonly TimeSpan MountCommandTimeout = TimeSpan.FromSeconds(5);

    public MountInfo? Detect(string folder)
    {
        if (OperatingSystem.IsWindows()) return FromDrive(folder);

        if (OperatingSystem.IsLinux())
        {
            var procMounts = ReadProcMounts();
            return procMounts == null ? null : FindMount(procMounts, ResolveLinks(folder));
        }

        if (OperatingSystem.IsMacOS())
        {
            var mountOutput = RunMountCommand();
            return mountOutput == null ? null : FindMacMount(mountOutput, ResolveLinks(folder));
        }

        return null;
    }

    /// <summary>
    /// The mount in /proc/mounts with the longest mount point that holds <paramref name="path"/>
    /// </summary>
    internal static MountInfo? FindMount(string procMounts, string path)
    {
        return FindLongest(ParseProcMounts(procMounts), path);
    }

    /// <summary>
    /// The mount in macOS' mount output with the longest mount point that holds <paramref name="path"/>
    /// </summary>
    internal static MountInfo? FindMacMount(string mountOutput, string path)
    {
        return FindLongest(ParseMacMounts(mountOutput), path);
    }

    private static MountInfo? FindLongest(IEnumerable<MountInfo> mounts, string path)
    {
        MountInfo? best = null;
        foreach (var mount in mounts)
        {
            var point = mount.Point;
            var inside = path == point || point == "/" || path.StartsWith(point.TrimEnd('/') + "/", StringComparison.Ordinal);
            // A later mount on the same point is stacked over the earlier one, e.g. cifs over systemd automount's autofs
            if (!inside || (best != null && point.Length < best.Point.Length)) continue;

            best = mount;
        }

        return best;
    }

    private static IEnumerable<MountInfo> ParseProcMounts(string procMounts)
    {
        foreach (var line in procMounts.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ');
            if (parts.Length < 3) continue;

            yield return new MountInfo(Unescape(parts[1]), Unescape(parts[0]), parts[2]);
        }
    }

    private static IEnumerable<MountInfo> ParseMacMounts(string mountOutput)
    {
        foreach (var line in mountOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = MacMountLine().Match(line);
            if (!match.Success) continue;

            yield return new MountInfo(match.Groups["point"].Value, match.Groups["source"].Value, match.Groups["type"].Value);
        }
    }

    // /proc/mounts writes space, tab, newline and backslash as octal escapes, e.g. \040 for a space
    [GeneratedRegex(@"\\([0-7]{3})")]
    private static partial Regex OctalEscape();

    // macOS writes "//joe@nas/Media on /Volumes/My Media (smbfs, nodev, nosuid, mounted by joe)", spaces unescaped
    [GeneratedRegex(@"^(?<source>.+?) on (?<point>.+) \((?<type>[^,)]+)(?:, [^)]*)?\)$")]
    private static partial Regex MacMountLine();

    private static string Unescape(string value) =>
        OctalEscape().Replace(value, m => ((char) Convert.ToInt32(m.Groups[1].Value, 8)).ToString());

    private static string? ReadProcMounts()
    {
        try
        {
            return File.ReadAllText(ProcMounts);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? RunMountCommand()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(MountCommand)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(MountCommandTimeout))
            {
                process.Kill();
                return null;
            }

            return output.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ResolveLinks(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? folder;
        }
        catch (Exception)
        {
            return folder;
        }
    }

    private static MountInfo? FromDrive(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            if (string.IsNullOrEmpty(root)) return null;

            var drive = new DriveInfo(root);
            return new MountInfo(drive.Name, drive.Name, drive.IsReady ? drive.DriveFormat : drive.DriveType.ToString());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
