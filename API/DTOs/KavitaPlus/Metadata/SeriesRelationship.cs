using API.Services.Plus;

namespace API.DTOs.KavitaPlus.Metadata;

public class SeriesRelationship
{
    public int AniListId { get; set; }
    public int? MalId { get; set; }
    //public MediaTitle SeriesName { get; set; }
    public ScrobbleProvider Provider { get; set; }

}
