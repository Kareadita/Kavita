using System.Collections.Generic;

namespace API.DTOs.Metadata
{
    /// <summary>
    /// Exclusively metadata about a given chapter
    /// </summary>
    public class ChapterMetadataDto
    {
        public int Id { get; set; }
        public int ChapterId { get; set; }
        public string Title { get; set; }
        public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> CoverArtists { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Characters { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Pencillers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Inkers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Colorists { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Letterers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Editors { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Translators { get; set; } = new List<PersonDto>();

        public ICollection<GenreTagDto> Genres { get; set; }
        /// <summary>
        /// Collection of all Tags from underlying chapters for a Series
        /// </summary>
        public ICollection<TagDto> Tags { get; set; }
        public AgeRatingDto AgeRating { get; set; }
        public string ReleaseYear { get; set; }
        public PublicationStatusDto PublicationStatus { get; set; }
        /// <summary>
        /// Summary for the Chapter/Issue
        /// </summary>
        public string Summary { get; set; }
        /// <summary>
        /// Language for the Chapter/Issue
        /// </summary>
        public string Language { get; set; }
        /// <summary>
        /// Number in the TotalCount of issues
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// Total number of issues for the series
        /// </summary>
        public int TotalCount { get; set; }

    }
}
