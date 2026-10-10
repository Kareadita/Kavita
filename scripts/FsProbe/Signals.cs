using System.Security.Cryptography;
using System.Text;

namespace FsProbe;

/// <summary>
/// Reads every change signal for one folder tree
/// </summary>
public static class Signals
{
    public sealed record Values(
        DateTime MaxFolderOwn,
        DateTime Current,
        DateTime Candidate,
        long? MaxChangeTicks,
        string NamesAndSizes);

    public static Values Read(string folder)
    {
        var maxFolderOwn = Directory.GetLastWriteTimeUtc(folder);
        var maxChange = ChangeTime.GetUtcTicks(folder);
        var names = new List<string>();

        foreach (var info in WriteTime.Enumerate(folder).OrderBy(i => i.FullName, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(folder, info.FullName);
            if (info is DirectoryInfo)
            {
                var own = Directory.GetLastWriteTimeUtc(info.FullName);
                if (own > maxFolderOwn) maxFolderOwn = own;
                names.Add(relative + "/");
            }
            else
            {
                names.Add($"{relative}|{((FileInfo) info).Length}");
            }

            var change = ChangeTime.GetUtcTicks(info.FullName);
            if (change != null && (maxChange == null || change > maxChange)) maxChange = change;
        }

        return new Values(
            maxFolderOwn,
            WriteTime.Current(folder),
            WriteTime.Candidate(folder),
            maxChange,
            Hash(string.Join('\n', names)));
    }

    /// <summary>
    /// Same rule as Kavita: changed when later than the last scan at second precision. MaxValue (missing or empty folder) counts as changed
    /// </summary>
    public static bool IsAfter(DateTime value, DateTime since) =>
        value == DateTime.MaxValue || WriteTime.TruncateToSecond(value) > WriteTime.TruncateToSecond(since);

    public static bool IsAfter(long? ticks, DateTime since) =>
        ticks != null && IsAfter(new DateTime(ticks.Value, DateTimeKind.Utc), since);

    public static SignalValues ToReport(Values values, TargetFile? target) => new()
    {
        MaxFolderOwnUtc = values.MaxFolderOwn.ToString("O"),
        CurrentUtc = values.Current == DateTime.MaxValue ? "MaxValue" : values.Current.ToString("O"),
        CandidateUtc = values.Candidate == DateTime.MaxValue ? "MaxValue" : values.Candidate.ToString("O"),
        MaxChangeTimeUtc = values.MaxChangeTicks is { } t ? WriteTime.Iso(t) : null,
        NamesAndSizes = values.NamesAndSizes,
        Target = target,
    };

    public static TargetFile ReadTarget(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return new TargetFile { Exists = false };

        var listing = new DirectoryInfo(info.DirectoryName!)
            .EnumerateFiles(info.Name)
            .FirstOrDefault();
        var change = ChangeTime.GetUtcTicks(path);
        return new TargetFile
        {
            Exists = true,
            PerFileUtc = File.GetLastWriteTimeUtc(path).ToString("O"),
            ListingUtc = listing?.LastWriteTimeUtc.ToString("O"),
            Size = info.Length,
            ChangeTimeUtc = change is { } t ? WriteTime.Iso(t) : null,
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
}
