using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kavita.API.Services.Scanner;

/// <remarks>Only <see cref="IFolderWatchService"/> drives this</remarks>
public interface INativeFileWatcherService
{
    /// <summary>
    /// Watches exactly these folders from now on
    /// </summary>
    /// <returns>When to try again, if a folder cannot be reached or watched and no retry is pending yet</returns>
    Task<TimeSpan?> WatchAsync(IReadOnlyList<string> folders);
    void Stop();
    /// <summary>
    /// A watcher event was raised in this folder, or in a folder created above it, at or after <paramref name="sinceUtc"/>
    /// </summary>
    /// <param name="folder">Normalized, as the scanner's read folders are</param>
    bool SawChange(string folder, DateTime sinceUtc);
    /// <summary>
    /// From when every event in this folder is known: its watcher's start, or the last prune of its library folder if later.
    /// Null when no watcher covers it
    /// </summary>
    DateTime? WatchingSince(string folder);
    /// <summary>
    /// Forgets the events under <paramref name="libraryFolder"/> older than <paramref name="beforeUtc"/>
    /// </summary>
    void Prune(string libraryFolder, DateTime beforeUtc);
}
