using System.Collections.Generic;

namespace API.DTOs
{
    public class UpdateSeriesMetadataDto
    {
        public SeriesMetadataDto SeriesMetadata { get; set; }
        public ICollection<CollectionTagDto> Tags { get; set; }
    }
}