using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace WatchProbe;

/// <summary>
/// PhysicalFileProvider in polling mode, watching the whole tree. A change token only says that something changed
/// </summary>
public sealed class ProviderSource(EventLog log) : IDisposable
{
    public const string Filter = "**/*";

    private PhysicalFileProvider? _provider;
    private IDisposable? _registration;

    public string? StartError { get; private set; }
    public bool Started => _registration != null;

    public void Start(string root)
    {
        try
        {
            var provider = new PhysicalFileProvider(root, ExclusionFilters.None)
            {
                UsePollingFileWatcher = true,
                UseActivePolling = true,
            };
            _provider = provider;
            _registration = ChangeToken.OnChange(() => provider.Watch(Filter), () => log.Add(EventLog.Provider, "Fired"));
        }
        catch (Exception ex)
        {
            StartError = $"{ex.GetType().Name}: {ex.Message}";
            _provider?.Dispose();
            _provider = null;
        }
    }

    public void Dispose()
    {
        _registration?.Dispose();
        _provider?.Dispose();
    }
}
