using System.ComponentModel;

namespace Kavita.Models.Entities.Enums;

/// <summary>
/// How a library actually notices changes, resolved from its <see cref="FolderWatchMode"/> and its mounts
/// </summary>
public enum FolderWatchMethod
{
    [Description("Off")]
    Off = 0,
    [Description("File Watcher")]
    FileWatcher = 1,
    [Description("Polling")]
    Polling = 2,
}
