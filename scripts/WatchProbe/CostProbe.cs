using System.Diagnostics;

namespace WatchProbe;

/// <summary>
/// Times several ways of polling the same tree, read-only
/// </summary>
public static class CostProbe
{
    private const string FolderNames = "folder-names";
    private const string FolderTimes = "folder-times";
    private const string KnownFolders = "known-folders";
    private const string Full = "full";

    private sealed record Pass(int Entries, int Errors);

    public static CostReport Run(EnvironmentInfo environment, int runs, TimeSpan gap, bool includeFull)
    {
        var root = environment.Path;
        var report = new CostReport { Environment = environment, GapSeconds = (int) gap.TotalSeconds };

        Console.WriteLine("Finding the folders once (not timed)...");
        var folders = ListFolderNames(root);
        report.Folders = folders.Count;
        Console.WriteLine($"  {folders.Count} folders");

        List<string> methods = [FolderNames, FolderTimes, KnownFolders];
        if (includeFull)
        {
            methods.Add(Full);
        }

        var total = runs * methods.Count;
        Console.WriteLine($"Timing {total} passes, {gap.TotalSeconds:F0} s apart so the mount's attribute cache has expired before each one");
        for (var run = 1; run <= runs; run++)
        {
            var order = run % 2 == 1 ? methods : Enumerable.Reverse(methods).ToList();
            foreach (var method in order)
            {
                Thread.Sleep(gap);
                var timing = Time(method, run, root, folders);
                report.Timings.Add(timing);
                if (method == Full)
                {
                    report.Files = timing.Entries - folders.Count;
                }
                Console.WriteLine($"  run {run} {method,-14} {timing.Entries,9} entries in {timing.Milliseconds / 1000,8:F1} s, {timing.Errors} errors");
            }
        }

        return report;
    }

    public static void PrintSummary(CostReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"Summary: {report.Folders} folders{(report.Files is { } files ? $", {files} files" : "")}");
        foreach (var group in report.Timings.GroupBy(t => t.Method))
        {
            var seconds = string.Join(" / ", group.Select(t => $"{t.Milliseconds / 1000:F1}"));
            Console.WriteLine($"  {group.Key,-14} {seconds} s");
        }
    }

    private static CostTiming Time(string method, int run, string root, List<string> folders)
    {
        var sw = Stopwatch.StartNew();
        var pass = method switch
        {
            FolderNames => new Pass(ListFolderNames(root).Count, 0),
            FolderTimes => ReadFolderTimes(root),
            KnownFolders => ReadKnownFolderTimes(root, folders),
            _ => ReadFull(root),
        };
        sw.Stop();

        return new CostTiming
        {
            Method = method,
            Run = run,
            Milliseconds = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
            Entries = pass.Entries,
            Errors = pass.Errors,
        };
    }

    private static List<string> ListFolderNames(string root) =>
        Directory.EnumerateDirectories(root, "*", FileListing.Options(true)).ToList();

    private static Pass ReadFolderTimes(string root)
    {
        var rootInfo = new DirectoryInfo(root);
        _ = rootInfo.LastWriteTimeUtc;
        var count = 0;
        foreach (var folder in rootInfo.EnumerateDirectories("*", FileListing.Options(true)))
        {
            count++;
            _ = folder.LastWriteTimeUtc;
        }
        return new Pass(count, 0);
    }

    private static Pass ReadKnownFolderTimes(string root, List<string> folders)
    {
        var errors = 0;
        Directory.GetLastWriteTimeUtc(root);
        foreach (var folder in folders)
        {
            // A missing folder is not an exception here: .NET returns 1601-01-01
            if (Directory.GetLastWriteTimeUtc(folder).Year == 1601)
            {
                errors++;
            }
        }
        return new Pass(folders.Count, errors);
    }

    private static Pass ReadFull(string root)
    {
        var count = 0;
        foreach (var info in FileListing.Enumerate(root))
        {
            count++;
            _ = info.LastWriteTimeUtc;
        }
        return new Pass(count, 0);
    }
}
