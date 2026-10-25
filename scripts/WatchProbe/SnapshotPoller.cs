using System.Diagnostics;

namespace WatchProbe;

/// <summary>
/// Lists the tree every interval and diffs name, size and write time against the previous listing
/// </summary>
public sealed class SnapshotPoller(EventLog log)
{
    private readonly record struct Entry(bool IsFolder, long Size, long WriteTicks);

    public sealed record Change(string Path, bool IsFolder, string Kind, string? Detail, double PreviousPollSeconds, double DetectedSeconds);

    private readonly Lock _lock = new();
    private readonly List<double> _pollMs = [];
    private readonly List<Change> _changes = [];
    private Dictionary<string, Entry> _last = [];
    private int _errors;

    public TreeStats ListBaseline(string root)
    {
        var sw = Stopwatch.StartNew();
        _last = List(root);
        return new TreeStats
        {
            Files = _last.Values.Count(e => !e.IsFolder),
            Folders = _last.Values.Count(e => e.IsFolder),
            ListingMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
        };
    }

    public async Task RunAsync(string root, TimeSpan interval, CancellationToken ct)
    {
        var previousPoll = log.Now;
        while (true)
        {
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var started = log.Now;
            var sw = Stopwatch.StartNew();
            Dictionary<string, Entry> current;
            try
            {
                current = List(root);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _errors++;
                }
                log.Add(EventLog.Poller, "Error", detail: $"{ex.GetType().Name}: {ex.Message}");
                continue;
            }

            lock (_lock)
            {
                _pollMs.Add(Math.Round(sw.Elapsed.TotalMilliseconds, 1));
            }
            Diff(current, previousPoll);
            _last = current;
            previousPoll = started;
        }
    }

    private void Diff(Dictionary<string, Entry> current, double previousPoll)
    {
        foreach (var (path, now) in current)
        {
            if (!_last.TryGetValue(path, out var before))
            {
                Record(path, now.IsFolder, "Added", Describe(now), previousPoll);
                continue;
            }

            var parts = new List<string>();
            if (before.Size != now.Size && !now.IsFolder)
            {
                parts.Add($"size {before.Size} -> {now.Size}");
            }
            if (before.WriteTicks != now.WriteTicks)
            {
                parts.Add($"mtime {Iso(before.WriteTicks)} -> {Iso(now.WriteTicks)}");
            }
            if (parts.Count == 0) continue;

            Record(path, now.IsFolder, "Changed", (now.IsFolder ? "folder, " : "") + string.Join(", ", parts), previousPoll);
        }

        foreach (var (path, before) in _last)
        {
            if (current.ContainsKey(path)) continue;
            Record(path, before.IsFolder, "Removed", Describe(before), previousPoll);
        }
    }

    private void Record(string path, bool isFolder, string kind, string detail, double previousPoll)
    {
        log.Add(EventLog.Poller, kind, path, detail);
        lock (_lock)
        {
            _changes.Add(new Change(path, isFolder, kind, detail, previousPoll, log.Now));
        }
    }

    public List<Change> Changes()
    {
        lock (_lock)
        {
            return [.. _changes];
        }
    }

    public PollerStats Stats()
    {
        lock (_lock)
        {
            var sorted = _pollMs.Order().ToList();
            return new PollerStats
            {
                Polls = sorted.Count,
                Errors = _errors,
                MinMs = sorted.Count == 0 ? null : sorted[0],
                MedianMs = sorted.Count == 0 ? null : sorted[sorted.Count / 2],
                MaxMs = sorted.Count == 0 ? null : sorted[^1],
            };
        }
    }

    private static Dictionary<string, Entry> List(string root)
    {
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var rootInfo = new DirectoryInfo(Path.TrimEndingDirectorySeparator(root));
        result[rootInfo.FullName] = new Entry(true, 0, rootInfo.LastWriteTimeUtc.Ticks);
        foreach (var info in FileListing.Enumerate(root))
        {
            var size = info is FileInfo file ? file.Length : 0;
            result[info.FullName] = new Entry(info is DirectoryInfo, size, info.LastWriteTimeUtc.Ticks);
        }
        return result;
    }

    private static string Describe(Entry entry) => entry.IsFolder ? "folder" : $"{entry.Size} bytes";

    private static string Iso(long ticks) => new DateTime(ticks, DateTimeKind.Utc).ToString("O");
}
