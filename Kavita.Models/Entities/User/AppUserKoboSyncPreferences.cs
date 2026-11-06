using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.Entities.User;

public class AppUserKoboSyncPreferences
{
    /// <summary>
    /// Maximum amount of items per sync (per type)
    /// </summary>
    [Range(25, 100)]
    public int SyncItemLimit { get; set; } = 100;
    /// <summary>
    /// Should requests be forwarded to the official kobo store
    /// </summary>
    public bool ForwardToKoboStore { get; set; } = true;
    /// <summary>
    /// When <see cref="SyncCollection"/> is unset, and true all chapters are synced to Kobo
    /// </summary>
    public bool AllLibraries { get; set; }

    /// <summary>
    /// When <see cref="SyncCollection"/> is unset, <see cref="AllLibraries"/> ,is false only chapters from series in
    /// these libraries are synced to Kobo
    /// </summary>
    public List<int> SyncLibraries { get; set; } = [];
    /// <summary>
    /// When set (!=0) only chapters from series in this collection are synced to Kobo
    /// </summary>
    public int SyncCollection { get; set; }

    public bool IsSyncingCollection => SyncCollection > 0;
}
