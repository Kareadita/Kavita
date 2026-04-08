using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.DTOs.ReadingLists;
#nullable enable

public sealed record ReadingListDto : IHasCoverImage
{
    public int Id { get; init; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    /// <summary>
    /// Reading lists that are promoted are only done by admins
    /// </summary>
    public bool Promoted { get; set; }
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// This is used to tell the UI if it should request a Cover Image or not. If null or empty, it has not been set.
    /// </summary>
    public string? CoverImage { get; set; } = string.Empty;

    public string? PrimaryColor { get; set; } = string.Empty;
    public string? SecondaryColor { get; set; } = string.Empty;

    /// <summary>
    /// Number of Items in the Reading List
    /// </summary>
    public int ItemCount { get; set; }

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
    /// <summary>
    /// The highest age rating from all Series within the reading list
    /// </summary>
    public required AgeRating AgeRating { get; set; } = AgeRating.Unknown;

    /// <summary>
    /// Username of the User that owns (in the case of a promoted list)
    /// </summary>
    public string OwnerUserName { get; set; }


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
    /// Determines how the list was created and if it's syncable.
    /// </summary>
    public ReadingListProvider Provider { get; set; } = ReadingListProvider.None;
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

    public bool CanSync => Provider != ReadingListProvider.None
                           && Provider != ReadingListProvider.File
                           && (!string.IsNullOrEmpty(SourcePath) || !string.IsNullOrEmpty(DownloadUrl));
    /// <summary>
    /// Checks if the remote SHA differs from our stored hash.
    /// </summary>
    public bool HasRemoteChange(string remoteSha)
        => !string.Equals(ShaHash, remoteSha, StringComparison.Ordinal);

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }

}
