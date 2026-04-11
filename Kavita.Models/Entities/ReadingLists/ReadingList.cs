using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.ReadingLists;

#nullable enable

/// <summary>
/// This is a collection of <see cref="ReadingListItem"/> which represent individual chapters and an order.
/// </summary>
public class ReadingList : IEntityDate, IHasCoverImage, IHasTags<ReadingListTag>
{
    public int Id { get; init; }
    public required string Title { get; set; }
    /// <summary>
    /// A normalized string used to check if the reading list already exists in the DB
    /// </summary>
    public required string NormalizedTitle { get; set; }
    /// <summary>
    /// Promotion allows non-owners to view the list
    /// </summary>
    public bool Promoted { get; set; }
    public string? CoverImage { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    /// <summary>
    /// Denotes if the CoverImage has been overridden by the user. If so, it will not be updated during normal scan operations.
    /// </summary>
    public bool CoverImageLocked { get; set; }

    /// <summary>
    /// A list of tags associated with the list
    /// </summary>
    /// <remarks>Can be populated via API/UI or from CBLv2</remarks>
    public ICollection<ReadingListTag> Tags { get; set; } = new List<ReadingListTag>();


    /// <summary>
    /// Determines how the list was created and if it's syncable.
    /// </summary>
    public ReadingListProvider Provider { get; set; } = ReadingListProvider.None;

    /// <summary>
    /// The repo-relative path used as the stable sync key (e.g. "Marvel/Spider-Man.cbl").
    /// This is the primary identifier for re-fetching — more stable than DownloadUrl.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Cached raw download URL for convenience. Reconstructable from SourcePath if needed.
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Git SHA of the file content at last sync. Used for change detection only — if the
    /// remote SHA differs from this value, the file has changed upstream.
    /// </summary>
    public string? ShaHash { get; set; }

    /// <summary>
    /// When we last checked the remote for changes (compared SHA). This can happen
    /// without downloading — a metadata-only check via the Contents API.
    /// </summary>
    public DateTime? LastSyncCheckUtc { get; set; }

    /// <summary>
    /// When we last actually downloaded and applied the CBL content.
    /// Only updated when ShaHash changes and we pull new content.
    /// </summary>
    public DateTime? LastSyncedUtc { get; set; }
    /// <summary>
    /// Total items at CBL Import, this helps track missing items. CBL Sync will update to the latest.
    /// </summary>
    public int TotalItemsAtImport { get; set; }

    public bool CanSync => Provider != ReadingListProvider.None
                           && Provider != ReadingListProvider.File
                           && (!string.IsNullOrEmpty(SourcePath) || !string.IsNullOrEmpty(DownloadUrl));

    /// <summary>
    /// Checks if the remote SHA differs from our stored hash.
    /// </summary>
    public bool HasRemoteChange(string remoteSha)
        => !string.Equals(ShaHash, remoteSha, StringComparison.Ordinal);

    public ICollection<ReadingListItem> Items { get; set; } = null!;

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }



    #region Metadata
    public string? Summary { get; set; }
    /// <summary>
    /// The highest age rating from all Series within the reading list
    /// </summary>
    public required AgeRating AgeRating { get; set; } = AgeRating.Unknown;
    /// <summary>
    /// Minimum Year the Reading List starts
    /// </summary>
    public int StartingYear { get; set; }
    /// <summary>
    /// Minimum Month the Reading List starts
    /// </summary>
    public int StartingMonth { get; set; }
    /// <summary>
    /// Maximum Year the Reading List starts
    /// </summary>
    public int EndingYear { get; set; }
    /// <summary>
    /// Maximum Month the Reading List starts
    /// </summary>
    public int EndingMonth { get; set; }
    #endregion

    // Relationships
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
