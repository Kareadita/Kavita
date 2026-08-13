#nullable enable
using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

/// <summary>
/// This is AniListSeries
/// </summary>
public sealed record ExternalSeriesDetailDto
{
    public string Name { get; set; }
    public ALMediaTitle Titles { get; set; } = new();
    /// <summary>
    /// Every known title, grouped by normalized BCP-47 language tag ("en", "ja", "ja-Latn", "pt-BR", "zh-HK").
    /// Each list is ordered best-first, so a client honoring a language preference can take [0] and stop.
    /// Empty for providers that do not expose per-language titles.
    /// </summary>
    /// <remarks>v3 only.</remarks>
    public Dictionary<string, IList<LocalizedTitleDto>> LocalizedTitles { get; set; } = [];
    public int? AniListId { get; set; }
    public long? MALId { get; set; }
    /// <summary>
    /// ComicBookRoundup Id for direct matching
    /// </summary>
    public int? CbrId { get; set; }
    public int? HardcoverId { get; set; }
    public bool IsStandAlone { get; set; }
    public int? MangabakaId { get; set; }
    public IList<string> Synonyms { get; set; } = [];
    [EnumDataType(typeof(PlusMediaFormat))]
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
    [EnumDataType(typeof(AgeRating))]
    public AgeRating AgeRating { get; set; } = AgeRating.Unknown;
    /// <summary>
    /// Raw content rating string for manual mapping via <see cref="MetadataSettingsDto.ExternalAgeRatingMappings"/>
    /// </summary>
    public string? AgeRatingRaw { get; set; }
    [EnumDataType(typeof(ScrobbleProvider))]
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
