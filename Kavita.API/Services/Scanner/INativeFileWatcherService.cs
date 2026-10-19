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
    /// <remarks>This is public only because Hangfire will invoke it</remarks>
    void UpdateLastBufferOverflow();
}
