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

    #region Sync sources

    /// <summary>
    /// If true, uses libraries as a source to sync from
    /// </summary>
    public bool LibrarySyncEnabled { get; set; }
    /// <summary>
    /// If <see cref="LibrarySyncEnabled"/> and not empty, only syncs these libraries
    /// </summary>
    public List<int> SyncLibraries { get; set; } = [];

    /// <summary>
    /// If true, uses reading lists as a source to sync from
    /// </summary>
    public bool ReadingListSyncEnabled { get; set; }
    /// <summary>
    /// If <see cref="ReadingListSyncEnabled"/> and not empty, only syncs from these reading lists (For reading lists source)
    /// </summary>
    public List<int> SyncReadingLists { get; set; } = [];

    /// <summary>
    /// If true, uses collections as a source to sync from
    /// </summary>
    public bool CollectionSyncEnabled { get; set; }
    /// <summary>
    /// If <see cref="CollectionSyncEnabled"/> and not empty, only syncs from these collections
    /// </summary>
    public List<int> SyncCollections { get; set; } = [];

    /// <summary>
    /// A collection used for adding recommendations. Always in union with the rest
    /// </summary>
    public int ManagedCollection { get; set; } = 0;

    public bool HasManagedCollection => ManagedCollection > 0;

    #endregion
}
