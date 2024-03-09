using System.Collections.Generic;

namespace API.DTOs.SeriesDetail;

public class UpdateRelatedSeriesDto
{
    public int SeriesId { get; set; }
    public IList<int> Adaptations { get; set; } = default!;
    public IList<int> Characters { get; set; } = default!;
    public IList<int> Contains { get; set; } = default!;
    public IList<int> Others { get; set; } = default!;
    public IList<int> Prequels { get; set; } = default!;
    public IList<int> Sequels { get; set; } = default!;
    public IList<int> SideStories { get; set; } = default!;
    public IList<int> SpinOffs { get; set; } = default!;
    public IList<int> AlternativeSettings { get; set; } = default!;
    public IList<int> AlternativeVersions { get; set; } = default!;
    public IList<int> Doujinshis { get; set; } = default!;
    public IList<int> Editions { get; set; } = default!;
    public IList<int> Annuals { get; set; } = default!;
}
