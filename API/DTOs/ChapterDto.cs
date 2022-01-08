using System;
using System.Collections.Generic;
using API.DTOs.Metadata;
using API.Entities;

namespace API.DTOs
{
    /// <summary>
    /// A Chapter is the lowest grouping of a reading medium. A Chapter contains a set of MangaFiles which represents the underlying
    /// file (abstracted from type).
    /// </summary>
    public class ChapterDto
    {
        public int Id { get; init; }
        /// <summary>
        /// Range of chapters. Chapter 2-4 -> "2-4". Chapter 2 -> "2".
        /// </summary>
        public string Range { get; init; }
        /// <summary>
        /// Smallest number of the Range.
        /// </summary>
        public string Number { get; init; }
        /// <summary>
        /// Total number of pages in all MangaFiles
        /// </summary>
        public int Pages { get; init; }
        /// <summary>
        /// If this Chapter contains files that could only be identified as Series or has Special Identifier from filename
        /// </summary>
        public bool IsSpecial { get; init; }
        /// <summary>
        /// Used for books/specials to display custom title. For non-specials/books, will be set to <see cref="Range"/>
        /// </summary>
        public string Title { get; init; }
        /// <summary>
        /// The files that represent this Chapter
        /// </summary>
        public ICollection<MangaFileDto> Files { get; init; }
        /// <summary>
        /// Calculated at API time. Number of pages read for this Chapter for logged in user.
        /// </summary>
        public int PagesRead { get; set; }
        /// <summary>
        /// If the Cover Image is locked for this entity
        /// </summary>
        public bool CoverImageLocked { get; set; }
        /// <summary>
        /// Volume Id this Chapter belongs to
        /// </summary>
        public int VolumeId { get; init; }
        /// <summary>
        /// When chapter was created
        /// </summary>
        public DateTime Created { get; init; }
        /// <summary>
        /// When the chapter was released.
        /// </summary>
        /// <remarks>Metadata field</remarks>
        public DateTime ReleaseDate { get; init; }
        /// <summary>
        /// Title of the Chapter/Issue
        /// </summary>
        /// <remarks>Metadata field</remarks>
        public string TitleName { get; set; }
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
        public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Penciller { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Inker { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Colorist { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Letterer { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> CoverArtist { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Editor { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Publisher { get; set; } = new List<PersonDto>();
        public ICollection<PersonDto> Translators { get; set; } = new List<PersonDto>();
        public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
    }
}
