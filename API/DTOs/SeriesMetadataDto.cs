using System.Collections.Generic;
using API.DTOs.CollectionTags;
using API.Entities;

namespace API.DTOs
{
    public class SeriesMetadataDto
    {
        public int Id { get; set; }
        public ICollection<string> Genres { get; set; }
        public ICollection<CollectionTagDto> Tags { get; set; }
        public ICollection<Person> Persons { get; set; }
        public string Publisher { get; set; }
        public int SeriesId { get; set; }
    }
}