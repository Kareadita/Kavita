using System.Collections.Generic;

namespace API.DTOs.SeriesDetail;

public class RelatedSeriesDto
{
    /// <summary>
    /// The parent relationship Series
    /// </summary>
    public int SourceSeriesId { get; set; }

    public IEnumerable<SeriesDto> Sequels { get; set; } = default!;
    public IEnumerable<SeriesDto> Prequels { get; set; } = default!;
    public IEnumerable<SeriesDto> SpinOffs { get; set; } = default!;
    public IEnumerable<SeriesDto> Adaptations { get; set; } = default!;
    public IEnumerable<SeriesDto> SideStories { get; set; } = default!;
    public IEnumerable<SeriesDto> Characters { get; set; } = default!;
    public IEnumerable<SeriesDto> Contains { get; set; } = default!;
    public IEnumerable<SeriesDto> Others { get; set; } = default!;
    public IEnumerable<SeriesDto> AlternativeSettings { get; set; } = default!;
    public IEnumerable<SeriesDto> AlternativeVersions { get; set; } = default!;
    public IEnumerable<SeriesDto> Doujinshis { get; set; } = default!;
    public IEnumerable<SeriesDto> Parent { get; set; } = default!;
    public IEnumerable<SeriesDto> Editions { get; set; } = default!;
    public IEnumerable<SeriesDto> Annuals { get; set; } = default!;
}
