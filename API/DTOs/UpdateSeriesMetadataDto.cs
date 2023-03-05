using System.Collections.Generic;
using API.DTOs.CollectionTags;

namespace API.DTOs;

public class UpdateSeriesMetadataDto
{
    public SeriesMetadataDto SeriesMetadata { get; set; } = default!;
    public ICollection<CollectionTagDto> CollectionTags { get; set; } = default!;
}
