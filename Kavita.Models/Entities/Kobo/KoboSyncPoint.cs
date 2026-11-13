using System;
using System.Collections.Generic;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.Kobo;

public class KoboSyncPoint
{

    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; }

    [ExportScrub(ExportScrubKind.Random)]
    public string KoboDeviceId { get; set; }

    /// <summary>
    /// Sync token of the upstream Kobo store, used when forwarding requests
    /// </summary>
    [ExportScrub(ExportScrubKind.Random)]
    public string? KoboStoreSyncToken { get; set; }

    /// <summary>
    /// Chapters removed on the Kobo, filtered out during sync. Can be un-removed in the UI
    /// </summary>
    [JsonColumn]
    public List<RemovedChapter> RemovedChapterIds { get; set; }

    /// <summary>
    /// All chapter ids in the last sync
    /// </summary>
    public List<int> ChapterIds { get; set; } = [];
    /// <summary>
    /// Amount of already processed changes in the current sync.
    /// </summary>
    public int ChapterIndex { get; set; }
    /// <summary>
    /// The time at which chapters were synced
    /// </summary>
    public DateTime ChapterSyncTime { get; set; }

    public DateTime ProgressSyncTime { get; set; }
    public int ProgressSyncedUntilId { get; set; }
    public int ProgressSyncedUntilIndex { get; set; }


    public DateTime CollectionsSyncTime { get; set; }
    public List<int> CollectionsIds { get; set; } = [];
    public int CollectionsSyncedUntilIndex { get; set; }

    public DateTime ReadingListsSyncTime { get; set; }
    public List<int> ReadingListsIds { get; set; } = [];
    public int ReadingListsSyncedUntilIndex { get; set; }

    public void Reset()
    {
        ChapterIndex = 0;
        ProgressSyncedUntilIndex = 0;
        CollectionsSyncedUntilIndex = 0;
        ReadingListsSyncedUntilIndex = 0;
        ChapterSyncTime = DateTime.UtcNow;
        ProgressSyncTime = DateTime.UtcNow;
        CollectionsSyncTime = DateTime.UtcNow;
        ReadingListsSyncTime = DateTime.UtcNow;
    }

}

public sealed class RemovedChapter
{
    public int ChapterId { get; set; }
    public DateTime RemovedAtUtc { get; set; }
}
