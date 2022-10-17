using System.Collections.Generic;

namespace API.DTOs.SeriesDetail;

public class UpdateRelatedSeriesDto
{
    public int SeriesId { get; set; }
    public IList<int> Adaptations { get; set; }
    public IList<int> Characters { get; set; }
    public IList<int> Contains { get; set; }
    public IList<int> Others { get; set; }
    public IList<int> Prequels { get; set; }
    public IList<int> Sequels { get; set; }
    public IList<int> SideStories { get; set; }
    public IList<int> SpinOffs { get; set; }
    public IList<int> AlternativeSettings { get; set; }
    public IList<int> AlternativeVersions { get; set; }
    public IList<int> Doujinshis { get; set; }
    public IList<int> Editions { get; set; }
}
