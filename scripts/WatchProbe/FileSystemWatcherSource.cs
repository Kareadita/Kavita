namespace WatchProbe;

/// <summary>
/// Set up like Kavita's LibraryWatcher.TryStartWatcher. Renamed is extra: Kavita does not subscribe to it
/// </summary>
public sealed class FileSystemWatcherSource(EventLog log) : IDisposable
{
    private FileSystemWatcher? _watcher;

    public string? StartError { get; private set; }
    public bool Started => _watcher != null;

    public void Start(string root)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(root);
            watcher.Changed += OnEvent;
            watcher.Created += OnEvent;
            watcher.Deleted += OnEvent;
            watcher.Renamed += (_, e) => log.Add(EventLog.Watcher, "Renamed", e.FullPath, oldPath: e.OldFullPath);
            watcher.Error += (_, e) => log.Add(EventLog.Watcher, "Error", detail: Describe(e.GetException()));

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex)
        {
            StartError = Describe(ex);
            watcher?.Dispose();
        }
    }

    private void OnEvent(object sender, FileSystemEventArgs e) => log.Add(EventLog.Watcher, e.ChangeType.ToString(), e.FullPath);

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";

    public void Dispose() => _watcher?.Dispose();
}
