using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Entities;
#nullable enable

/// <summary>
/// What folder watching learned about each mount the library's folders sit on, stored as JSON on <see cref="Library.FolderWatchState"/>
/// </summary>
public sealed record LibraryFolderWatchState
{
    public IReadOnlyList<MountEntry> Mounts { get; init; } = [];
    public DowngradeMarker? Downgrade { get; init; }

    /// <summary>
    /// How the library's folders are actually checked for changes: the library's setting, then what was learned about its mounts
    /// </summary>
    public FolderWatchMethod WatchMethod(FolderWatchMode mode) => mode switch
    {
        FolderWatchMode.Off => FolderWatchMethod.Off,
        FolderWatchMode.Polling => FolderWatchMethod.Polling,
        _ when Downgrade != null => FolderWatchMethod.Failed,
        FolderWatchMode.FileWatcher => FolderWatchMethod.FileWatcher,
        _ when Mounts.Any(m => m.Result is MountResult.Failed) => FolderWatchMethod.Failed,
        _ when Mounts.Any(m => m.Result is MountResult.Network) => FolderWatchMethod.Polling,
        _ when Mounts.Any(m => m.Result is MountResult.Trial) => FolderWatchMethod.Trial,
        _ => FolderWatchMethod.FileWatcher,
    };

    public sealed record MountEntry
    {
        /// <summary>
        /// SHA-256 of the mount's source and type, so a share's address is not stored
        /// </summary>
        public required string Fingerprint { get; init; }
        public MountResult Result { get; init; }
        /// <summary>
        /// Audited scans that confirmed at least one changed folder on this mount
        /// </summary>
        public int Successes { get; init; }
        /// <summary>
        /// Library scans whose audit found no miss on this mount
        /// </summary>
        public int AuditedScans { get; init; }
    }

    /// <summary>
    /// The library polls until an admin presses Retry or the folder's mount changes
    /// </summary>
    public sealed record DowngradeMarker
    {
        public DowngradeReason Reason { get; init; }
        public required string Folder { get; init; }
        public string? Fingerprint { get; init; }
        public DateTime AtUtc { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<DowngradeReason>))]
    public enum DowngradeReason
    {
        /// <summary>
        /// The folder's watcher raised 3 errors within 10 minutes
        /// </summary>
        Errors,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<MountResult>))]
    public enum MountResult
    {
        /// <summary>
        /// A mount type file system events cannot see changes from other machines on, polled from the start
        /// </summary>
        Network,
        /// <summary>
        /// Watched until the scan audit confirms or rejects it
        /// </summary>
        Trial,
        Watching,
        Failed,
    }
}
