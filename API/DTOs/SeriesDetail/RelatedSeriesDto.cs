using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.SeriesDetail;

public class RelatedSeriesDto
{
    /// <summary>
    /// The parent relationship Series
    /// </summary>
    public int SourceSeriesId { get; set; }

    public IEnumerable<SeriesDto> Sequels { get; set; }
    public IEnumerable<SeriesDto> Prequels { get; set; }
    public IEnumerable<SeriesDto> SpinOffs { get; set; }
    public IEnumerable<SeriesDto> Adaptations { get; set; }
    public IEnumerable<SeriesDto> SideStories { get; set; }
    public IEnumerable<SeriesDto> Characters { get; set; }
    public IEnumerable<SeriesDto> Contains { get; set; }
    public IEnumerable<SeriesDto> Others { get; set; }
}
