using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.Entities;

namespace Kavita.API.Services.Scanner;

public interface IFolderWatchService
{
    /// <summary>
    /// Start watching or polling all library folders
    /// </summary>
    Task StartWatching();
    /// <summary>
    /// Stop watching and polling all folders
    /// </summary>
    void StopWatching();
    /// <summary>
    /// Essentially stops then starts watching. Useful if there is a change in folders or libraries
    /// </summary>
    Task RestartWatching();
    /// <summary>
    /// Every watched library with this folder polls until Retry or until the folder is on another mount
    /// </summary>
    /// <remarks>This is public only because Hangfire will invoke it</remarks>
    Task DowngradeFolder(string folder, LibraryFolderWatchState.DowngradeReason reason);
    /// <summary>
    /// Checks the watcher saw each folder a non-forced scan found changed. A miss makes its mount Failed
    /// </summary>
    /// <param name="readFolders">Normalized, from the scan's folder listing</param>
    Task AuditScanAsync(int libraryId, IReadOnlyCollection<string> readFolders, DateTime scanStartedUtc, bool isLibraryScan);
    /// <summary>
    /// Failed mounts go back to Trial and a downgrade is cleared, so the library watches again
    /// </summary>
    Task RetryAsync(int libraryId);
}
