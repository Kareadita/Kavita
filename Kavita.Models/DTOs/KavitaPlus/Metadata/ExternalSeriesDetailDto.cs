#nullable enable
using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

/// <summary>
/// This is AniListSeries
/// </summary>
public sealed record ExternalSeriesDetailDto
{
    public string Name { get; set; }
    public ALMediaTitle Titles { get; set; } = new();
    public Dictionary<string, IList<LocalizedTitleDto>> LocalizedTitles { get; set; } = [];
    public int? AniListId { get; set; }
    public long? MALId { get; set; }
    public ALMediaTitle Titles { get; set; } = new();
    public Dictionary<string, IList<LocalizedTitleDto>> LocalizedTitles { get; set; } = [];
    /// <summary>
    /// ComicBookRoundup Id for direct matching
    /// </summary>
    public int? CbrId { get; set; }
    public int? HardcoverId { get; set; }
    public bool IsStandAlone { get; set; }
    public int? MangabakaId { get; set; }
    public IList<string> Synonyms { get; set; } = [];
    public PlusMediaFormat PlusMediaFormat { get; set; }
    public string? SiteUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IList<string> Genres { get; set; }
    public IList<SeriesStaffDto> Staff { get; set; }
    public IList<MetadataTagDto> Tags { get; set; }
    public string? Summary { get; set; }

    /// <summary>
    /// Base age rating derived by the provider from its content rating. Kavita raises this via its own
    /// tag/genre mappings, then applies the requesting user's age restriction before returning drill-down detail.
    /// </summary>
    /// <remarks>Unknown when the provider did not supply a mappable content rating.</remarks>
    public AgeRating AgeRating { get; set; } = AgeRating.Unknown;
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.AniList;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int AverageScore { get; set; }
    /// <remarks>AniList returns the total count of unique chapters, includes 1.1 for example</remarks>
    public int Chapters { get; set; }
    /// <remarks>AniList returns the total count of unique volumes, includes 1.1 for example</remarks>
    public int Volumes { get; set; }
    public IList<SeriesRelationship>? Relations { get; set; } = [];
    public IList<SeriesCharacter>? Characters { get; set; } = [];
    public IList<RatingDto> Ratings { get; set; } = new List<RatingDto>();

    public string? Publisher { get; set; }

    public IList<ExternalChapterDto>? ChapterDtos { get; set; }

    public IList<ExternalEditionDto> Editions { get; set; } = [];

}

public sealed record LocalizedTitleDto
{
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The provider's preferred title within this language.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// An officially licensed title, as opposed to a fan or community translation.
    /// </summary>
    public bool IsOfficial { get; init; }
}
