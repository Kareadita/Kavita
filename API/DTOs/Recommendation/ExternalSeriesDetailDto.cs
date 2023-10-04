using System.Collections.Generic;

namespace API.DTOs.Recommendation;

public class ExternalSeriesDetailDto
{
    public string Name { get; set; }
    public int? AniListId { get; set; }
    public long? MALId { get; set; }
    public IList<string> Synonyms { get; set; }
    public PlusMediaFormat PlusMediaFormat { get; set; }
    public string? SiteUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IList<string> Genres { get; set; }
    public IList<SeriesStaffDto> Staff { get; set; }
    public IList<MetadataTagDto> Tags { get; set; }
    public string? Summary { get; set; }
    public int? VolumeCount { get; set; }
    public int? ChapterCount { get; set; }
}
