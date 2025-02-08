using API.Entities.Enums;
using API.Entities.Metadata;
using API.Services.Plus;

namespace API.Entities;

public class SeriesMetadataPeople
{
    public int SeriesMetadataId { get; set; }
    public virtual SeriesMetadata SeriesMetadata { get; set; }

    public int PersonId { get; set; }
    public virtual Person Person { get; set; }

    /// <summary>
    /// The source of this connection. If not Kavita, this implies Metadata Download linked this and it can be removed between matches
    /// </summary>
    public bool KavitaPlusConnection { get; set; } = false;
    /// <summary>
    /// A weight that allows lower numbers to sort first
    /// </summary>
    public int OrderWeight { get; set; }

    public required PersonRole Role { get; set; }
}
