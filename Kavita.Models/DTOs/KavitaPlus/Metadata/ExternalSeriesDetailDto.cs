#nullable enable
using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

/// <summary>
/// This is AniListSeries
/// </summary>
public sealed record ExternalSeriesDetailDto
{
    public string Name { get; set; }
    public int? AniListId { get; set; }
    public long? MALId { get; set; }
    /// <summary>
    /// ComicBookRoundup Id for direct matching
    /// </summary>
    public int? CbrId { get; set; }
    public int? HardcoverId { get; set; }
    public int? MangabakaId { get; set; }
    public IList<string> Synonyms { get; set; } = [];
    public PlusMediaFormat PlusMediaFormat { get; set; }
    public string? SiteUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IList<string> Genres { get; set; }
    public IList<SeriesStaffDto> Staff { get; set; }
    public IList<MetadataTagDto> Tags { get; set; }
    public string? Summary { get; set; }
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

    #region Comic Only
    public string? Publisher { get; set; }
    /// <summary>
    /// Only from CBR for <see cref="ScrobbleProvider.Cbr"/>. Full metadata about issues
    /// </summary>
    public IList<ExternalChapterDto>? ChapterDtos { get; set; }
    #endregion


}
