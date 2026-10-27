using System;
using System.Collections.Generic;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.Kobo;

public class KoboSyncPoint
{

    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; }

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

    public void Reset()
    {
        ChapterIndex = 0;
        ChapterSyncTime = DateTime.UtcNow;
        ProgressSyncTime = DateTime.Now;
    }

}
