using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.SignalR;

/// <summary>
/// ScanFolder jobs waiting to run, one per library and trigger
/// </summary>
public sealed record ScheduledFolderScanDto
{
    /// <summary>
    /// Null when the folder is in no library
    /// </summary>
    public int? LibraryId { get; init; }
    /// <summary>
    /// False when requested through the API
    /// </summary>
    public bool FromFolderWatcher { get; init; }
    /// <summary>
    /// Soonest job, null once one is queued to run
    /// </summary>
    public DateTime? RunAtUtc { get; init; }
    /// <summary>
    /// Soonest first, at most <see cref="MaxFolders"/> of <see cref="FolderCount"/>
    /// </summary>
    public required IList<string> Folders { get; init; }
    public int FolderCount { get; init; }

    public const int MaxFolders = 5;
}
