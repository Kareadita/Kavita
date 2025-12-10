using System;
using System.Collections.Generic;
using API.DTOs.Metadata;
using API.DTOs.Person;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.DTOs;
#nullable enable

/// <summary>
/// A Chapter is the lowest grouping of a reading medium. A Chapter contains a set of MangaFiles which represents the underlying
/// file (abstracted from type).
/// </summary>
public class ChapterDto : IHasReadTimeEstimate, IHasCoverImage
{
    /// <inheritdoc cref="API.Entities.Chapter.Id"/>
    public int Id { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.Range"/>
    public string Range { get; init; } = default!;
    /// <inheritdoc cref="API.Entities.Chapter.Number"/>
    [Obsolete("Use MinNumber and MaxNumber instead")]
    public string Number { get; init; } = default!;
    /// <inheritdoc cref="API.Entities.Chapter.MinNumber"/>
    public float MinNumber { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.MaxNumber"/>
    public float MaxNumber { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.SortOrder"/>
    public float SortOrder { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.Pages"/>
    public int Pages { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.IsSpecial"/>
    public bool IsSpecial { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.Title"/>
    public string Title { get; set; } = default!;
    /// <summary>
    /// The files that represent this Chapter
    /// </summary>
    public ICollection<MangaFileDto> Files { get; init; } = default!;
    /// <summary>
    /// Calculated at API time. Number of pages read for this Chapter for logged-in user.
    /// </summary>
    public int PagesRead { get; set; }
    /// <summary>
    /// Total number of complete reads
    /// </summary>
    /// <remarks>Calculated at API-time</remarks>
    public int TotalReads { get; set; }
    /// <summary>
    /// The last time a chapter was read by current authenticated user
    /// </summary>
    public DateTime LastReadingProgressUtc { get; set; }
    /// <summary>
    /// The last time a chapter was read by current authenticated user
    /// </summary>
    public DateTime LastReadingProgress { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.CoverImageLocked"/>
    public bool CoverImageLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.VolumeId"/>
    public int VolumeId { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.CreatedUtc"/>
    public DateTime CreatedUtc { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.LastModifiedUtc"/>
    public DateTime LastModifiedUtc { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.Created"/>
    public DateTime Created { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.ReleaseDate"/>
    public DateTime ReleaseDate { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.TitleName"/>
    public string TitleName { get; set; } = default!;
    /// <inheritdoc cref="API.Entities.Chapter.Summary"/>
    public string Summary { get; init; } = default!;
    /// <inheritdoc cref="API.Entities.Chapter.AgeRating"/>
    public AgeRating AgeRating { get; init; }
    /// <inheritdoc cref="API.Entities.Chapter.WordCount"/>
    public long WordCount { get; set; } = 0L;
    /// <summary>
    /// Formatted Volume title ie) Volume 2.
    /// </summary>
    /// <remarks>Only available when fetched from Series Detail API</remarks>
    public string VolumeTitle { get; set; } = string.Empty;
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public float AvgHoursToRead { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.WebLinks"/>
    public string WebLinks { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.ISBN"/>
    public string ISBN { get; set; }

    #region Metadata

    public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> CoverArtists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Characters { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Pencillers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Inkers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Imprints { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Colorists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Letterers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Editors { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Translators { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Teams { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Locations { get; set; } = new List<PersonDto>();

    public ICollection<GenreTagDto> Genres { get; set; } = new List<GenreTagDto>();

    /// <summary>
    /// Collection of all Tags from underlying chapters for a Series
    /// </summary>
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
    public PublicationStatus PublicationStatus { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.Language"/>
    public string? Language { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.Count"/>
    public int Count { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.TotalCount"/>
    public int TotalCount { get; set; }

    /// <inheritdoc cref="API.Entities.Chapter.LanguageLocked"/>
    public bool LanguageLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.SummaryLocked"/>
    public bool SummaryLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.AgeRatingLocked"/>
    public bool AgeRatingLocked { get; set; }
    public bool PublicationStatusLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.GenresLocked"/>
    public bool GenresLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.TagsLocked"/>
    public bool TagsLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.WriterLocked"/>
    public bool WriterLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.CharacterLocked"/>
    public bool CharacterLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.ColoristLocked"/>
    public bool ColoristLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.EditorLocked"/>
    public bool EditorLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.InkerLocked"/>
    public bool InkerLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.ImprintLocked"/>
    public bool ImprintLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.LettererLocked"/>
    public bool LettererLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.PencillerLocked"/>
    public bool PencillerLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.PublisherLocked"/>
    public bool PublisherLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.TranslatorLocked"/>
    public bool TranslatorLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.TeamLocked"/>
    public bool TeamLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.LocationLocked"/>
    public bool LocationLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.CoverArtistLocked"/>
    public bool CoverArtistLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.ReleaseDateLocked"/>
    public bool ReleaseDateLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.TitleNameLocked"/>
    public bool TitleNameLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.SortOrderLocked"/>
    public bool SortOrderLocked { get; set; }

    #endregion

    /// <inheritdoc cref="API.Entities.Chapter.CoverImage"/>
    public string? CoverImage { get; set; }
    /// <inheritdoc cref="API.Entities.Chapter.PrimaryColor"/>
    public string? PrimaryColor { get; set; } = string.Empty;
    /// <inheritdoc cref="API.Entities.Chapter.SecondaryColor"/>
    public string? SecondaryColor { get; set; } = string.Empty;

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
