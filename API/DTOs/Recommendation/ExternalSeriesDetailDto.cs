using System.Collections.Generic;
using API.DTOs.Scrobbling;
using API.Services.Plus;

namespace API.DTOs.Recommendation;
#nullable enable

public class ExternalSeriesDetailDto
{
    public string Name { get; set; }
    public int? AniListId { get; set; }
    public long? MALId { get; set; }
    public IList<string> Synonyms { get; set; }
    public MediaFormat PlusMediaFormat { get; set; }
    public string? SiteUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IList<string> Genres { get; set; }
    public IList<SeriesStaffDto> Staff { get; set; }
    public IList<MetadataTagDto> Tags { get; set; }
    public string? Summary { get; set; }
    public int? VolumeCount { get; set; }
    public int? ChapterCount { get; set; }
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.AniList;
}
