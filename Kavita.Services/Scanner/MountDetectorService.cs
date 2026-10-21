using System;
using System.IO;
using System.Text.RegularExpressions;
using Kavita.API.Services.Scanner;
using Kavita.Models.Scanner;

namespace Kavita.Services.Scanner;

public partial class MountDetectorService : IMountDetectorService
{
    private const string ProcMounts = "/proc/mounts";

    public MountInfo? Detect(string folder)
    {
        if (!OperatingSystem.IsLinux()) return OperatingSystem.IsWindows() ? FromDrive(folder) : null;

        string text;
        try
        {
            text = File.ReadAllText(ProcMounts);
        }
        catch (Exception)
        {
            return null;
        }

        return FindMount(text, ResolveLinks(folder));
    }

    /// <summary>
    /// The mount with the longest mount point that holds <paramref name="path"/>
    /// </summary>
    internal static MountInfo? FindMount(string procMounts, string path)
    {
        MountInfo? best = null;
        foreach (var line in procMounts.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ');
            if (parts.Length < 3) continue;

            var point = Unescape(parts[1]);
            var inside = path == point || point == "/" || path.StartsWith(point.TrimEnd('/') + "/", StringComparison.Ordinal);
            // A later mount on the same point is stacked over the earlier one, e.g. cifs over systemd automount's autofs
            if (!inside || (best != null && point.Length < best.Point.Length)) continue;

            best = new MountInfo(point, Unescape(parts[0]), parts[2]);
        }

        return best;
    }

    // /proc/mounts writes space, tab, newline and backslash as octal escapes, e.g. \040 for a space
    [GeneratedRegex(@"\\([0-7]{3})")]
    private static partial Regex OctalEscape();

    private static string Unescape(string value) =>
        OctalEscape().Replace(value, m => ((char) Convert.ToInt32(m.Groups[1].Value, 8)).ToString());

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
