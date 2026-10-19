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

    public FolderWatchMethod Method(FolderWatchMode mode) => mode switch
    {
        FolderWatchMode.Off => FolderWatchMethod.Off,
        FolderWatchMode.FileWatcher => FolderWatchMethod.FileWatcher,
        FolderWatchMode.Polling => FolderWatchMethod.Polling,
        _ => Mounts.Any(m => m.Result is MountResult.Network or MountResult.Failed)
            ? FolderWatchMethod.Polling
            : FolderWatchMethod.FileWatcher,
    };

    public sealed record MountEntry
    {
        /// <summary>
        /// SHA-256 of the mount's source and type, so a share's address is not stored
        /// </summary>
        public required string Fingerprint { get; init; }
        public MountResult Result { get; init; }
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
