using System.Collections.Generic;

namespace API.DTOs.Metadata
{
    public class ChapterMetadataDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        //public ICollection<GenreTagDto> Genres { get; set; }
        public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Penciller { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Inker { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Colorist { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Letterer { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> CoverArtist { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Editor { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Publisher { get; set; } = new List<PersonDto>();
        public int ChapterId { get; set; }
    }
}
