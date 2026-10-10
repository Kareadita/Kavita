using System.Diagnostics;

namespace FsProbe;

/// <summary>
/// Read-only. Compares the listing timestamp to the per-file call for every entry, and times both implementations
/// </summary>
public static class PassiveProbe
{
    private const int MaxExamples = 20;

    public static PassiveReport Run(string root, EnvironmentInfo environment, int runs, bool readChangeTime)
    {
        var report = new PassiveReport { Environment = environment };

        // Untimed walk first, otherwise whichever method runs first pays for the cold cache
        WriteTime.Current(root);

        DateTime current = default, candidate = default;
        for (var run = 1; run <= runs; run++)
        {
            if (run % 2 == 1)
            {
                current = Time(report, "current", run, () => WriteTime.Current(root));
                candidate = Time(report, "candidate", run, () => WriteTime.Candidate(root));
            }
            else
            {
                candidate = Time(report, "candidate", run, () => WriteTime.Candidate(root));
                current = Time(report, "current", run, () => WriteTime.Current(root));
            }
        }
        report.RootResultsMatchTruncated = WriteTime.TruncateToSecond(current) == WriteTime.TruncateToSecond(candidate);

        var paths = CompareEntries(root, report);
        if (readChangeTime && ChangeTime.Supported) report.ChangeTime = ReadChangeTimes(paths, report);

        return report;
    }

    private static DateTime Time(PassiveReport report, string method, int run, Func<DateTime> action)
    {
        var sw = Stopwatch.StartNew();
        var result = action();
        sw.Stop();
        report.Timings.Add(new MethodTiming
        {
            Method = method,
            Run = run,
            Milliseconds = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
            ResultUtc = result == DateTime.MaxValue ? "MaxValue" : result.ToString("O"),
        });
        Console.WriteLine($"  run {run} {method,-9} {sw.Elapsed.TotalMilliseconds,10:F1} ms");
        return result;
    }

    private static List<(string Path, long Mtime)> CompareEntries(string root, PassiveReport report)
    {
        var parity = report.Parity;
        var affectedFolders = new HashSet<string>();
        var files = new List<(string, long)>();
        var future = DateTime.UtcNow.AddDays(1);
        var oldest = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var info in WriteTime.Enumerate(root))
        {
            var isFolder = info is DirectoryInfo;
            var listing = info.LastWriteTimeUtc;
            var perFile = isFolder ? Directory.GetLastWriteTimeUtc(info.FullName) : File.GetLastWriteTimeUtc(info.FullName);

            if (isFolder) report.Folders++;
            else
            {
                report.Files++;
                files.Add((info.FullName, perFile.Ticks));
            }
            if (perFile > future) report.FutureMtime++;
            if (perFile < oldest) report.MtimeBefore1990++;

            parity.Compared++;
            if (listing == perFile) continue;

            parity.MismatchRaw++;
            var deltaMs = (listing - perFile).TotalMilliseconds;
            if (Math.Abs(deltaMs) > Math.Abs(parity.MaxDeltaMs)) parity.MaxDeltaMs = deltaMs;
            if (WriteTime.TruncateToSecond(listing) == WriteTime.TruncateToSecond(perFile)) continue;

            parity.MismatchTruncated++;
            affectedFolders.Add(Path.GetDirectoryName(info.FullName) ?? "");
            if (parity.Examples.Count < MaxExamples)
            {
                parity.Examples.Add(new ParityExample
                {
                    Path = info.FullName,
                    PerFileUtc = perFile.ToString("O"),
                    ListingUtc = listing.ToString("O"),
                    DeltaMs = deltaMs,
                });
            }
        }

        parity.FoldersAffected = affectedFolders.Count;
        return files;
    }

    private static ChangeTimeResult ReadChangeTimes(List<(string Path, long Mtime)> files, PassiveReport report)
    {
        var result = new ChangeTimeResult();
        var sw = Stopwatch.StartNew();
        foreach (var (path, mtime) in files)
        {
            var ctime = ChangeTime.GetUtcTicks(path);
            if (ctime == null)
            {
                result.Failed++;
                continue;
            }

            result.Read++;
            // ctime well after mtime means the mtime was set back: a copy that kept it, or a tagger that restored it
            if (ctime.Value - mtime <= 2 * TimeSpan.TicksPerSecond) continue;

            result.CtimeAfterMtime++;
            if (result.Examples.Count < MaxExamples)
            {
                result.Examples.Add(new ChangeTimeExample
                {
                    Path = path,
                    MtimeUtc = WriteTime.Iso(mtime),
                    CtimeUtc = WriteTime.Iso(ctime.Value),
                });
            }
        }
        sw.Stop();

        report.Timings.Add(new MethodTiming
        {
            Method = "change-time (files only)",
            Run = 1,
            Milliseconds = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
        });
        Console.WriteLine($"  change time         {sw.Elapsed.TotalMilliseconds,10:F1} ms ({files.Count} files)");
        return result;
    }
}
