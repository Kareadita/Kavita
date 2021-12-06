using System.Collections.Generic;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.Entities.Enums;

namespace API.DTOs
{
    public class SeriesMetadataDto
    {
        public int Id { get; set; }
        public string Summary { get; set; }
        public ICollection<CollectionTagDto> Tags { get; set; }
        public ICollection<GenreTagDto> Genres { get; set; }
        public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Artists { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Characters { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Pencillers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Inkers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Colorists { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Letterers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Editors { get; set; } = new List<PersonDto>();
        public AgeRating AgeRating { get; set; } = AgeRating.Unknown;

        public int SeriesId { get; set; }
    }
}
