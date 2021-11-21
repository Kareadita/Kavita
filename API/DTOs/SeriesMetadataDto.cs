using System.Collections.Generic;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.Entities;

namespace API.DTOs
{
    public class SeriesMetadataDto
    {
        public int Id { get; set; }
        public ICollection<CollectionTagDto> Tags { get; set; }
        public ICollection<GenreTagDto> Genres { get; set; }
        public ICollection<Person> Persons { get; set; }
        public string Publisher { get; set; }
        public int SeriesId { get; set; }
    }
}
