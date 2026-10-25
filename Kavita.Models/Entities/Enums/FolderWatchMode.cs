using System.ComponentModel;

namespace Kavita.Models.Entities.Enums;

/// <summary>
/// How a library notices files added, changed or removed between scheduled scans
/// </summary>
public enum FolderWatchMode
{
    /// <summary>
    /// Changes wait for the next scheduled or manual scan
    /// </summary>
    [Description("Off")]
    Off = 0,
    /// <summary>
    /// Kavita picks file system events or polling for each mount
    /// </summary>
    [Description("Automatic")]
    Automatic = 1,
    /// <summary>
    /// Always file system events
    /// </summary>
    [Description("File Watcher")]
    FileWatcher = 2,
    /// <summary>
    /// Always polling of the library's folders
    /// </summary>
    [Description("Polling")]
    Polling = 3,
}
