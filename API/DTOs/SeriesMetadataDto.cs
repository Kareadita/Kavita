using System.Collections.Generic;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;

namespace API.DTOs
{
    public class SeriesMetadataDto
    {
        public int Id { get; set; }
        public ICollection<CollectionTagDto> Tags { get; set; }
        public ICollection<GenreTagDto> Genres { get; set; }
        public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
        public int SeriesId { get; set; }
    }
}
