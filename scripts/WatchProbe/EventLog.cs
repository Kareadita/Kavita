using System.Diagnostics;

namespace WatchProbe;

/// <summary>
/// Every source writes here from its own thread. Each event is printed as it arrives and kept for the report
/// </summary>
public sealed class EventLog(string root)
{
    public const string Watcher = "watcher";
    public const string Provider = "provider";
    public const string Poller = "poller";

    private const int MaxEvents = 20_000;

    private readonly Lock _lock = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<WatchEvent> _events = [];
    private int _dropped;

    public double Now => Math.Round(_clock.Elapsed.TotalSeconds, 3);

    public void Add(string source, string kind, string? path = null, string? detail = null, string? oldPath = null)
    {
        var item = new WatchEvent
        {
            Source = source,
            Kind = kind,
            Seconds = Now,
            AtUtc = DateTime.UtcNow.ToString("O"),
            Path = path,
            OldPath = oldPath,
            Detail = detail,
        };

        lock (_lock)
        {
            if (_events.Count < MaxEvents)
            {
                _events.Add(item);
            }
            else
            {
                _dropped++;
            }
        }

        var was = oldPath == null ? "" : $" (was {Relative(oldPath)})";
        var extra = detail == null ? "" : $"  {detail}";
        Console.WriteLine($"  [{item.Seconds,8:F1}s] {source,-8} {kind,-8} {Relative(path)}{was}{extra}");
    }

    private string? Relative(string? path) => path == null ? null : Path.GetRelativePath(root, path);

    public void Note(string message) => Console.WriteLine($"  [{Now,8:F1}s] {message}");

    public List<WatchEvent> Events()
    {
        lock (_lock)
        {
            return [.. _events];
        }
    }

    public int Dropped
    {
        get
        {
            lock (_lock)
            {
                return _dropped;
            }
        }
    }
}
