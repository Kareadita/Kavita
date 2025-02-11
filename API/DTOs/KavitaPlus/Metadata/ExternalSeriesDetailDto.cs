using System;
using System.Collections.Generic;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Scrobbling;
using API.Services.Plus;

namespace API.DTOs.Recommendation;
#nullable enable

/// <summary>
/// This is AniListSeries
/// </summary>
public class ExternalSeriesDetailDto
{
    public string Name { get; set; }
    public int? AniListId { get; set; }
    public long? MALId { get; set; }
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
    public int Chapters { get; set; }
    public int Volumes { get; set; }
    public IList<SeriesRelationship>? Relations { get; set; } = [];
    public IList<SeriesCharacter>? Characters { get; set; } = [];


}
