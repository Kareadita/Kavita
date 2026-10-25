namespace WatchProbe;

/// <summary>
/// Runs the FileSystemWatcher, the provider and the poller side by side on one folder
/// </summary>
public sealed class WatchSession
{
    private const double MatchSlackSeconds = 10;
    private static readonly TimeSpan CheckpointEvery = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SlowListing = TimeSpan.FromSeconds(30);

    private sealed record ControlAction(string Action, double At);

    private readonly string _root;
    private readonly EnvironmentInfo _environment;
    private readonly TimeSpan _interval;
    private readonly bool _runControl;
    private readonly EventLog _log;
    private readonly SnapshotPoller _poller;
    private readonly FileSystemWatcherSource _watcher;
    private readonly ProviderSource _provider;

    private readonly Lock _lock = new();
    private TreeStats _tree = new();
    private string? _controlFile;
    private string? _controlError;
    private readonly List<ControlAction> _controlActions = [];

    public WatchSession(EnvironmentInfo environment, TimeSpan interval, bool runControl)
    {
        _root = environment.Path;
        _environment = environment;
        _interval = interval;
        _runControl = runControl;
        _log = new EventLog(_root);
        _poller = new SnapshotPoller(_log);
        _watcher = new FileSystemWatcherSource(_log);
        _provider = new ProviderSource(_log);
    }

    public async Task RunAsync(Action checkpoint, CancellationToken ct)
    {
        Console.WriteLine("Listing the folder once...");
        var tree = _poller.ListBaseline(_root);
        lock (_lock)
        {
            _tree = tree;
        }
        Console.WriteLine($"  {tree.Files} files, {tree.Folders} folders, listed in {tree.ListingMs / 1000:F1} s");
        if (tree.ListingMs > SlowListing.TotalMilliseconds)
        {
            Console.WriteLine($"  WARNING: one listing takes over {SlowListing.TotalSeconds:F0} s. The provider and the poller list the whole tree again and again, " +
                              "which keeps the share busy. Press Ctrl+C and point at a smaller folder (one series) unless you meant this");
        }

        _watcher.Start(_root);
        _provider.Start(_root);
        PrintStart("FileSystemWatcher", _watcher.Started, _watcher.StartError);
        PrintStart($"PhysicalFileProvider polling {ProviderSource.Filter}", _provider.Started, _provider.StartError);
        PrintStart($"poller every {_interval.TotalSeconds:F0} s", true, null);

        var polling = _poller.RunAsync(_root, _interval, ct);
        var checkpoints = CheckpointAsync(checkpoint, ct);

        try
        {
            if (_runControl)
            {
                await RunControlAsync(ct);
            }

            PrintInstructions();
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
        }

        await polling;
        await checkpoints;
        _watcher.Dispose();
        _provider.Dispose();
    }

    private async Task RunControlAsync(CancellationToken ct)
    {
        var path = Path.Combine(_root, $"watchprobe-control-{DateTime.UtcNow:yyyyMMddHHmmss}.bin");
        var wait = _interval * 2 + TimeSpan.FromSeconds(MatchSlackSeconds);
        lock (_lock)
        {
            _controlFile = path;
        }

        Console.WriteLine();
        Console.WriteLine($"Control: this program writes {path}, then deletes it {wait.TotalSeconds:F0} s later. Please wait");
        try
        {
            var createdAt = _log.Now;
            await File.WriteAllBytesAsync(path, new byte[16], ct);
            AddControl("create", createdAt);
            await Task.Delay(wait, ct);

            var deletedAt = _log.Now;
            File.Delete(path);
            AddControl("delete", deletedAt);
            await Task.Delay(wait, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_lock)
            {
                _controlError = $"{ex.GetType().Name}: {ex.Message}";
            }
            Console.WriteLine($"  control could not run: {_controlError}");
        }
        finally
        {
            TryDelete(path);
        }
    }

    private void AddControl(string action, double at)
    {
        lock (_lock)
        {
            _controlActions.Add(new ControlAction(action, at));
        }
    }

    private void PrintInstructions()
    {
        Console.WriteLine();
        Console.WriteLine($"Now make changes FROM ANOTHER MACHINE (the NAS, a downloader, your PC over the share), inside {_root}:");
        Console.WriteLine("  1. copy a new .cbz or .zip in");
        Console.WriteLine("  2. rename a file");
        Console.WriteLine("  3. delete a file");
        Console.WriteLine("  4. create a new folder and copy a file into it");
        Console.WriteLine("  5. copy a file over an existing one with the same name (Windows: \"Replace the file\")");
        Console.WriteLine($"Wait about {(_interval * 2).TotalSeconds + MatchSlackSeconds:F0} s between changes so they can be told apart.");
    }

    public WatchReport BuildReport(string endReason, double plannedMinutes)
    {
        var events = _log.Events();
        var watcherEvents = events.Where(e => e.Source == EventLog.Watcher && e.Path != null).ToList();
        var providerFires = events.Where(e => e.Source == EventLog.Provider).Select(e => e.Seconds).ToList();
        var changes = _poller.Changes();

        lock (_lock)
        {
            return new WatchReport
            {
                Environment = _environment,
                Inotify = InotifyLimits.Read(),
                IntervalSeconds = (int) _interval.TotalSeconds,
                PlannedMinutes = plannedMinutes,
                EndReason = endReason,
                RanSeconds = _log.Now,
                Tree = _tree,
                Sources =
                [
                    new SourceStatus { Name = "FileSystemWatcher", Started = _watcher.Started, Error = _watcher.StartError, Events = events.Count(e => e.Source == EventLog.Watcher) },
                    new SourceStatus { Name = "PhysicalFileProvider", Started = _provider.Started, Error = _provider.StartError, Events = providerFires.Count },
                    new SourceStatus { Name = "Poller", Started = true, Events = changes.Count },
                ],
                Control = BuildControl(watcherEvents, providerFires, changes),
                Poller = _poller.Stats(),
                Changes = changes.Select(c => new ChangeMatch
                {
                    Path = c.Path,
                    IsFolder = c.IsFolder,
                    Kind = c.Kind,
                    Detail = c.Detail,
                    PreviousPollSeconds = c.PreviousPollSeconds,
                    DetectedSeconds = c.DetectedSeconds,
                    WatcherSeconds = watcherEvents
                        .FirstOrDefault(e => IsAbout(e, c.Path) && e.Seconds >= c.PreviousPollSeconds && e.Seconds <= c.DetectedSeconds + MatchSlackSeconds)
                        ?.Seconds,
                    ProviderFired = providerFires.Any(s => s >= c.PreviousPollSeconds && s <= c.DetectedSeconds + MatchSlackSeconds),
                    ParentFolderSeconds = c is { IsFolder: true, Kind: "Changed" } ? null : ParentFolderSeconds(c, changes),
                }).ToList(),
                EventsDropped = _log.Dropped,
                Events = events,
            };
        }
    }

    /// <remarks>Caller must hold <see cref="_lock"/></remarks>
    private ControlResult? BuildControl(List<WatchEvent> watcherEvents, List<double> providerFires, List<SnapshotPoller.Change> changes)
    {
        if (_controlFile == null) return null;

        var result = new ControlResult { File = _controlFile, Error = _controlError };
        for (var i = 0; i < _controlActions.Count; i++)
        {
            var from = _controlActions[i].At;
            var until = i + 1 < _controlActions.Count ? _controlActions[i + 1].At : double.MaxValue;
            var path = _controlFile;

            result.Steps.Add(new ControlStep
            {
                Action = _controlActions[i].Action,
                AtSeconds = from,
                Watcher = Latency(watcherEvents.Where(e => IsAbout(e, path)).Select(e => e.Seconds), from, until),
                Provider = Latency(providerFires, from, until),
                Poller = Latency(changes.Where(c => c.Path == path).Select(c => c.DetectedSeconds), from, until),
            });
        }
        return result;
    }

    private static double? Latency(IEnumerable<double> seconds, double from, double until)
    {
        var first = seconds.Where(s => s >= from && s < until).DefaultIfEmpty(double.NaN).Min();
        return double.IsNaN(first) ? null : Math.Round(first - from, 3);
    }

    private static bool IsAbout(WatchEvent e, string path) => e.Path == path || e.OldPath == path;

    // NTFS can update a folder's time one poll after the file change shows up, so the next poll counts too
    private static double? ParentFolderSeconds(SnapshotPoller.Change change, List<SnapshotPoller.Change> changes)
    {
        var parent = Path.GetDirectoryName(change.Path);
        return changes
            .FirstOrDefault(p => p is { IsFolder: true, Kind: "Changed" } && p.Path == parent
                                 && p.PreviousPollSeconds >= change.PreviousPollSeconds && p.PreviousPollSeconds <= change.DetectedSeconds)
            ?.DetectedSeconds;
    }

    private static async Task CheckpointAsync(Action checkpoint, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(CheckpointEvery);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    checkpoint();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  could not save the report so far: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void PrintStart(string name, bool started, string? error) =>
        Console.WriteLine(started ? $"  started: {name}" : $"  FAILED to start: {name}: {error}");

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Left behind is harmless: Kavita ignores .bin files
        }
    }
}
