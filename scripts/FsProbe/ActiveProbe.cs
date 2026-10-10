namespace FsProbe;

/// <summary>
/// Makes real changes inside a temp folder and records which signals notice each one, at several delays
/// </summary>
public static class ActiveProbe
{
    private const int FileSize = 1000;

    private sealed class Context(string folder, DateTime old)
    {
        public string Folder { get; } = folder;
        public DateTime Old { get; } = old;
        public string Series => Path.Combine(Folder, "Series");
        public string Vol1 => Path.Combine(Series, "Vol 01.bin");
        public string Outside => Path.Combine(Folder, "outside.bin");
        public string In(string relative) => Path.Combine(Series, relative);
    }

    private sealed record Scenario(
        string Name,
        string Description,
        Func<Context, string?> Target,
        Action<Context>? Setup,
        Func<Context, IDisposable?> Act);

    private static readonly Scenario[] Scenarios =
    [
        new("add-file", "New file in the series folder",
            c => c.In("Vol 03.bin"), null,
            c => { Write(c.In("Vol 03.bin"), FileSize, 'c'); return null; }),

        new("delete-file", "Delete a file",
            _ => null, null,
            c => { File.Delete(c.Vol1); return null; }),

        new("rename-file", "Rename a file",
            c => c.In("Vol 01 (fixed).bin"), null,
            c => { File.Move(c.Vol1, c.In("Vol 01 (fixed).bin")); return null; }),

        new("rename-subfolder", "Rename a volume folder",
            _ => null, null,
            c => { Directory.Move(c.In("Vol 2"), c.In("Volume 2")); return null; }),

        new("overwrite-in-place", "Rewrite a file's content in place, new size",
            c => c.Vol1, null,
            c => { Write(c.Vol1, 1500, 'z'); return null; }),

        new("overwrite-same-size-keep-mtime", "Rewrite in place, same size, old modified time restored (tagger)",
            c => c.Vol1, null,
            c =>
            {
                var before = File.GetLastWriteTimeUtc(c.Vol1);
                Write(c.Vol1, FileSize, 'z');
                File.SetLastWriteTimeUtc(c.Vol1, before);
                return null;
            }),

        new("copy-over-keep-mtime", "Copy a fixed file over an existing one, keeping the source's old time (Explorer copy-over)",
            c => c.Vol1,
            c => Write(c.Outside, 1200, 'f', c.Old.AddDays(-1)),
            c =>
            {
                File.Copy(c.Outside, c.Vol1, overwrite: true);
                File.SetLastWriteTimeUtc(c.Vol1, File.GetLastWriteTimeUtc(c.Outside));
                return null;
            }),

        new("temp-then-rename", "Write a temp file, then rename it over the original",
            c => c.Vol1, null,
            c =>
            {
                var temp = c.Vol1 + ".tmp";
                Write(temp, 1300, 't');
                File.Move(temp, c.Vol1, overwrite: true);
                return null;
            }),

        new("add-in-subfolder", "New file in a volume subfolder",
            c => c.In(Path.Combine("Vol 2", "c02.bin")), null,
            c => { Write(c.In(Path.Combine("Vol 2", "c02.bin")), FileSize, 'c'); return null; }),

        new("add-keep-mtime", "New file that keeps an old modified time (rsync -a, cp -p)",
            c => c.In("Vol 03.bin"), null,
            c => { Write(c.In("Vol 03.bin"), FileSize, 'c', c.Old); return null; }),

        new("open-for-write", "File still open for writing at the first read, closed after it",
            c => c.Vol1, null,
            c =>
            {
                var stream = new FileStream(c.Vol1, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                stream.Write(new byte[500]);
                stream.Flush(flushToDisk: true);
                return stream;
            }),

        new("symlink-added", "New symlink to a file outside the folder",
            c => c.In("Link.bin"),
            c => Write(c.Outside, FileSize, 'o', c.Old),
            c => { File.CreateSymbolicLink(c.In("Link.bin"), c.Outside); return null; }),

        new("symlink-target-modified", "Existing symlink, its target outside the folder is rewritten",
            c => c.In("Link.bin"),
            c =>
            {
                Write(c.Outside, FileSize, 'o', c.Old);
                File.CreateSymbolicLink(c.In("Link.bin"), c.Outside);
            },
            c => { Write(c.Outside, 1400, 'n'); return null; }),
    ];

    public static ActiveReport Run(string dir, EnvironmentInfo environment, List<int> delays, bool keep)
    {
        var probeFolder = Path.Combine(dir, $".kavita-fsprobe-{DateTime.UtcNow:yyyyMMddHHmmss}");
        var report = new ActiveReport { Environment = environment, ProbeFolder = probeFolder, Delays = delays };

        Directory.CreateDirectory(probeFolder);
        try
        {
            report.ClockSkewMs = MeasureClockSkew(probeFolder);
            Console.WriteLine($"  clock skew (file time - local clock): {report.ClockSkewMs:F0} ms");

            var old = DateTime.UtcNow.AddDays(-1);
            var contexts = new List<(Scenario Scenario, Context Context, ScenarioResult Result, Signals.Values? Baseline)>();
            foreach (var scenario in Scenarios)
            {
                var context = new Context(Path.Combine(probeFolder, scenario.Name), old);
                var result = new ScenarioResult { Name = scenario.Name, Description = scenario.Description };
                try
                {
                    SetUp(context, scenario);
                }
                catch (Exception ex)
                {
                    result.Error = $"setup: {ex.GetType().Name}: {ex.Message}";
                }
                contexts.Add((scenario, context, result, null));
                report.Scenarios.Add(result);
            }

            for (var i = 0; i < contexts.Count; i++)
            {
                var (scenario, context, result, _) = contexts[i];
                if (result.Error != null) continue;
                var baseline = Signals.Read(context.Series);
                result.Baseline = Signals.ToReport(baseline, Target(scenario, context));
                contexts[i] = (scenario, context, result, baseline);
            }
            var baselineTime = DateTime.UtcNow;
            foreach (var (_, _, result, _) in contexts) result.BaselineUtc = baselineTime.ToString("O");

            // Two seconds keeps the change out of the baseline's second on filesystems with coarse times
            Thread.Sleep(2100);

            var pending = new List<IDisposable>();
            foreach (var (scenario, context, result, _) in contexts)
            {
                if (result.Error != null) continue;
                try
                {
                    var open = scenario.Act(context);
                    if (open != null) pending.Add(open);
                }
                catch (Exception ex)
                {
                    result.Error = $"act: {ex.GetType().Name}: {ex.Message}";
                }
            }
            var actedAt = DateTime.UtcNow;

            foreach (var delay in delays)
            {
                var wait = actedAt.AddSeconds(delay) - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    Console.WriteLine($"  waiting {wait.TotalSeconds:F0} s for the {delay} s read");
                    Thread.Sleep(wait);
                }

                foreach (var (scenario, context, result, baseline) in contexts)
                {
                    if (result.Error != null || baseline == null) continue;
                    result.Snapshots.Add(Take(scenario, context, baseline, baselineTime, delay));
                }

                foreach (var open in pending) open.Dispose();
                pending.Clear();
            }

            PrintTable(report);
        }
        finally
        {
            if (!keep) TryDelete(probeFolder);
        }

        return report;
    }

    private static void SetUp(Context context, Scenario scenario)
    {
        Directory.CreateDirectory(context.In("Vol 2"));
        Write(context.Vol1, FileSize, 'a', context.Old);
        Write(context.In(Path.Combine("Vol 2", "c01.bin")), FileSize, 'b', context.Old);
        scenario.Setup?.Invoke(context);

        // Folders last, writing a file inside bumps them
        TrySetFolderTime(context.In("Vol 2"), context.Old);
        TrySetFolderTime(context.Series, context.Old);
    }

    private static ScenarioSnapshot Take(Scenario scenario, Context context, Signals.Values baseline,
        DateTime baselineTime, int delay)
    {
        var values = Signals.Read(context.Series);
        return new ScenarioSnapshot
        {
            DelaySeconds = delay,
            TakenUtc = DateTime.UtcNow.ToString("O"),
            Caught = new Verdicts
            {
                FolderOnly = Signals.IsAfter(values.MaxFolderOwn, baselineTime),
                Current = Signals.IsAfter(values.Current, baselineTime),
                Candidate = Signals.IsAfter(values.Candidate, baselineTime),
                NamesAndSizes = values.NamesAndSizes != baseline.NamesAndSizes,
                ChangeTime = ChangeTime.Supported ? Signals.IsAfter(values.MaxChangeTicks, baselineTime) : null,
            },
            CandidateMatchesCurrent = WriteTime.TruncateToSecond(values.Current) == WriteTime.TruncateToSecond(values.Candidate),
            Values = Signals.ToReport(values, Target(scenario, context)),
        };
    }

    private static TargetFile? Target(Scenario scenario, Context context)
    {
        var target = scenario.Target(context);
        return target == null ? null : Signals.ReadTarget(target);
    }

    private static double? MeasureClockSkew(string probeFolder)
    {
        var path = Path.Combine(probeFolder, "clock.tmp");
        var before = DateTime.UtcNow;
        File.WriteAllBytes(path, [1]);
        var after = DateTime.UtcNow;
        var fileTime = File.GetLastWriteTimeUtc(path);
        File.Delete(path);
        var midpoint = before + (after - before) / 2;
        return Math.Round((fileTime - midpoint).TotalMilliseconds, 0);
    }

    private static void Write(string path, int size, char fill, DateTime? mtime = null)
    {
        File.WriteAllBytes(path, Enumerable.Repeat((byte) fill, size).ToArray());
        if (mtime != null) File.SetLastWriteTimeUtc(path, mtime.Value);
    }

    private static void TrySetFolderTime(string path, DateTime time)
    {
        try
        {
            Directory.SetLastWriteTimeUtc(path, time);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  could not set folder time on {path}: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  could not delete {path}, please remove it by hand: {ex.Message}");
        }
    }

    private static void PrintTable(ActiveReport report)
    {
        Console.WriteLine();
        Console.WriteLine("  Caught?  F = folder time only, K = Kavita today, C = candidate, S = names+sizes, T = change time");
        foreach (var delay in report.Delays)
        {
            Console.WriteLine($"  -- read at {delay} s --");
            foreach (var scenario in report.Scenarios)
            {
                if (scenario.Error != null)
                {
                    if (delay == report.Delays[0]) Console.WriteLine($"  {scenario.Name,-32} skipped: {scenario.Error}");
                    continue;
                }

                var snap = scenario.Snapshots.FirstOrDefault(s => s.DelaySeconds == delay);
                if (snap == null) continue;
                var c = snap.Caught;
                var t = c.ChangeTime switch { true => "T", false => "-", null => "?" };
                var parity = snap.CandidateMatchesCurrent ? "" : "  (candidate differs from Kavita today)";
                Console.WriteLine($"  {scenario.Name,-32} {Mark(c.FolderOnly, 'F')} {Mark(c.Current, 'K')} {Mark(c.Candidate, 'C')} {Mark(c.NamesAndSizes, 'S')} {t}{parity}");
            }
        }
    }

    private static char Mark(bool caught, char letter) => caught ? letter : '-';
}
