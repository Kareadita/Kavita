using API.Entities.Enums;
using API.Entities.Metadata;

namespace API.Entities;

public class SeriesMetadataPeople
{
    public int SeriesMetadataId { get; set; }
    public virtual SeriesMetadata SeriesMetadata { get; set; }

    public int PersonId { get; set; }
    public virtual Person Person { get; set; }

    public required PersonRole Role { get; set; }
}
