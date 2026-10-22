using System.Diagnostics;
using System.IO.Compression;

namespace WatchProbe;

/// <summary>
/// Writes a .cbz into a new folder, waits for the watcher to go quiet, then repeats the reads Kavita's validation and
/// scan make, one at a time, and records which of them the watcher reports as a change
/// </summary>
public static class SelfReadProbe
{
    private const string FileName = "selfread-a.cbz";
    private const string ExtraFileName = "selfread-b.cbz";
    private const string RenamedFileName = "selfread-c.cbz";
    private static readonly TimeSpan QuietMax = TimeSpan.FromSeconds(30);

    private sealed record Read(string Name, Func<string, string, long> Run);

    private static readonly IReadOnlyList<Read> Reads =
    [
        new("list-names", (dir, _) => Directory.EnumerateFileSystemEntries(dir).Count()),
        new("read-times", (dir, _) => new DirectoryInfo(dir).EnumerateFiles().Sum(f => f.Length + f.LastWriteTimeUtc.Ticks)),
        new("folder-time", (dir, _) => Directory.GetLastWriteTimeUtc(dir).Ticks),
        new("open-deny-writers", (_, file) =>
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Length;
        }),
        new("read-entries", (_, file) =>
        {
            using var zip = ZipFile.OpenRead(file);
            return zip.Entries.Count;
        }),
        new("read-all", (_, file) => File.ReadAllBytes(file).Length),
    ];

    public static SelfReadReport Run(EnvironmentInfo environment, TimeSpan pause)
    {
        var root = environment.Path;
        var report = new SelfReadReport { Environment = environment, PauseSeconds = pause.TotalSeconds };
        var log = new EventLog(root);
        using var watcher = new FileSystemWatcherSource(log);
        watcher.Start(root);
        if (!watcher.Started)
        {
            report.Error = watcher.StartError;
            return report;
        }

        var dir = Path.Combine(root, $"watchprobe-self-{DateTime.Now:yyyyMMdd-HHmmss}");
        var file = Path.Combine(dir, FileName);
        var extra = Path.Combine(dir, ExtraFileName);
        try
        {
            Directory.CreateDirectory(dir);
            WriteCbz(file);
            log.Note($"Wrote {FileName}, waiting for the watcher to go quiet");
            WaitQuiet(log, pause);

            foreach (var round in new[] { "after-write", "again" })
            {
                foreach (var read in Reads)
                {
                    report.Steps.Add(RunStep(log, root, round, read.Name, () => read.Run(dir, file), pause));
                }
                WaitQuiet(log, pause);
            }

            // A real change while Kavita reads the folder must still show up
            report.Steps.Add(RunStep(log, root, "real-change", "read-times+write", () =>
            {
                var result = Reads[1].Run(dir, file);
                WriteCbz(extra);
                return result;
            }, pause));
            WaitQuiet(log, pause);

            // Validation after a delete opens nothing, it only lists the folder
            report.Steps.Add(RunStep(log, root, "delete", "delete", () =>
            {
                File.Delete(extra);
                return 0;
            }, pause));
            WaitQuiet(log, pause);
            report.Steps.Add(RunStep(log, root, "after-delete", "read-times", () => Reads[1].Run(dir, file), pause));
            WaitQuiet(log, pause);

            // Kavita does not subscribe to Renamed, so a rename reaches it only through other events
            var renamed = Path.Combine(dir, RenamedFileName);
            report.Steps.Add(RunStep(log, root, "rename", "rename", () =>
            {
                File.Move(file, renamed);
                return 0;
            }, pause));
            WaitQuiet(log, pause);
            report.Steps.Add(RunStep(log, root, "after-rename", "read-times", () => Reads[1].Run(dir, renamed), pause));
        }
        catch (Exception ex)
        {
            report.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            report.Events = log.Events();
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not delete {dir}: {ex.Message}");
            }
        }

        return report;
    }

    private static SelfReadStep RunStep(EventLog log, string root, string round, string name, Func<long> run, TimeSpan pause)
    {
        log.Note($"{round}: {name}");
        var start = log.Now;
        var step = new SelfReadStep { Round = round, Name = name, StartSeconds = start };
        var sw = Stopwatch.StartNew();
        try
        {
            run();
        }
        catch (Exception ex)
        {
            step.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        step.DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);

        Thread.Sleep(pause);
        var end = log.Now;
        step.Events = log.Events()
            .Where(e => e.Source == EventLog.Watcher && e.Seconds >= start && e.Seconds < end)
            .Select(e => new SelfReadEvent
            {
                Kind = e.Kind,
                Path = e.Path == null ? "" : Path.GetRelativePath(root, e.Path),
                SecondsAfterStart = Math.Round(e.Seconds - start, 3),
            })
            .ToList();
        return step;
    }

    private static void WaitQuiet(EventLog log, TimeSpan quiet)
    {
        var started = log.Now;
        while (log.Now - started < QuietMax.TotalSeconds)
        {
            var last = log.Events().LastOrDefault(e => e.Source == EventLog.Watcher)?.Seconds ?? 0;
            if (log.Now - Math.Max(last, started) >= quiet.TotalSeconds) return;
            Thread.Sleep(200);
        }
    }

    private static void WriteCbz(string path)
    {
        var buffer = new byte[64 * 1024];
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var i = 0; i < 3; i++)
        {
            Random.Shared.NextBytes(buffer);
            using var entry = zip.CreateEntry($"page{i:D2}.jpg", CompressionLevel.NoCompression).Open();
            entry.Write(buffer);
        }
    }

    public static void PrintSummary(SelfReadReport report)
    {
        Console.WriteLine();
        Console.WriteLine("Summary: what the watcher reported after each step");
        if (report.Error != null)
        {
            Console.WriteLine($"  error: {report.Error}");
        }
        foreach (var step in report.Steps)
        {
            var events = step.Events.Count == 0
                ? "nothing"
                : string.Join(", ", step.Events.Select(e => $"{e.Kind} {e.Path} (+{e.SecondsAfterStart:F1} s)"));
            var error = step.Error == null ? "" : $"  [{step.Error}]";
            Console.WriteLine($"  {step.Round,-12} {step.Name,-18} {step.DurationMs,8:F1} ms  {events}{error}");
        }
    }
}
