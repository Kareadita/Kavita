using System;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.DTOs;
#nullable enable

public sealed record SeriesDto : IHasReadTimeEstimate, IHasCoverImage
{
    /// <inheritdoc cref="API.Entities.Series.Id"/>
    public int Id { get; init; }
    /// <inheritdoc cref="API.Entities.Series.Name"/>
    public string? Name { get; init; }
    /// <inheritdoc cref="API.Entities.Series.OriginalName"/>
    public string? OriginalName { get; init; }
    /// <inheritdoc cref="API.Entities.Series.LocalizedName"/>
    public string? LocalizedName { get; init; }
    /// <inheritdoc cref="API.Entities.Series.SortName"/>
    public string? SortName { get; init; }
    /// <inheritdoc cref="API.Entities.Series.Pages"/>
    public int Pages { get; init; }
    /// <inheritdoc cref="API.Entities.Series.CoverImageLocked"/>
    public bool CoverImageLocked { get; set; }

    /// <inheritdoc cref="API.Entities.Series.LastChapterAdded"/>
    public DateTime LastChapterAdded { get; set; }
    /// <inheritdoc cref="API.Entities.Series.LastChapterAddedUtc"/>
    public DateTime LastChapterAddedUtc { get; set; }


    #region Progress (applied on the fly)
    /// <summary>
    /// Rating from logged-in user
    /// </summary>
    /// <remarks>Calculated at API-time</remarks>
    public float UserRating { get; set; }
    /// <summary>
    /// If the user has set the rating or not
    /// </summary>
    /// <remarks>Calculated at API-time</remarks>
    public bool HasUserRated { get; set; }
    /// <summary>
    /// Min <see cref="ChapterDto.TotalReads"/> across the series
    /// </summary>
    /// <remarks>Calculated at API-time</remarks>
    public int TotalReads { get; set; }
    /// <summary>
    /// Sum of pages read from linked Volumes. Calculated at API-time.
    /// </summary>
    /// <remarks>Calculated at API-time</remarks>
    public int PagesRead { get; set; }
    /// <summary>
    /// DateTime representing last time the series was Read. Calculated at API-time.
    /// </summary>
    /// <remarks>Calculated at API-time</remarks>
    public DateTime LatestReadDate { get; set; }
    #endregion


    /// <inheritdoc cref="API.Entities.Series.Format"/>
    public MangaFormat Format { get; set; }
    /// <inheritdoc cref="API.Entities.Series.Created"/>
    public DateTime Created { get; set; }

    /// <inheritdoc cref="API.Entities.Series.SortNameLocked"/>
    public bool SortNameLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Series.LocalizedNameLocked"/>
    public bool LocalizedNameLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Series.WordCount"/>
    public long WordCount { get; set; }

    /// <inheritdoc cref="API.Entities.Series.LibraryId"/>
    public int LibraryId { get; set; }
    public string LibraryName { get; set; } = default!;
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public float AvgHoursToRead { get; set; }
    /// <inheritdoc cref="API.Entities.Series.FolderPath"/>
    public string FolderPath { get; set; } = default!;
    /// <inheritdoc cref="API.Entities.Series.LowestFolderPath"/>
    public string? LowestFolderPath { get; set; }
    /// <inheritdoc cref="API.Entities.Series.LastFolderScanned"/>
    public DateTime LastFolderScanned { get; set; }
    #region KavitaPlus
    /// <inheritdoc cref="API.Entities.Series.DontMatch"/>
    public bool DontMatch { get; set; }
    /// <inheritdoc cref="API.Entities.Series.IsBlacklisted"/>
    public bool IsBlacklisted { get; set; }
    #endregion

    #region ColorScape
    /// <inheritdoc cref="API.Entities.Series.CoverImage"/>
    public string? CoverImage { get; set; }
    /// <inheritdoc cref="API.Entities.Series.PrimaryColor"/>
    public string? PrimaryColor { get; set; } = string.Empty;
    /// <inheritdoc cref="API.Entities.Series.SecondaryColor"/>
    public string? SecondaryColor { get; set; } = string.Empty;
    #endregion

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }


}
