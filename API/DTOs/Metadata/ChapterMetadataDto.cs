using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Metadata;
#nullable enable

/// <summary>
/// Exclusively metadata about a given chapter
/// </summary>
[Obsolete("Will not be maintained as of v0.8.1")]
public class ChapterMetadataDto
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public string Title { get; set; } = default!;
    public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> CoverArtists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Characters { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Pencillers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Inkers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Imprints { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Colorists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Letterers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Editors { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Translators { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Teams { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Locations { get; set; } = new List<PersonDto>();

    public ICollection<GenreTagDto> Genres { get; set; } = new List<GenreTagDto>();

    /// <summary>
    /// Collection of all Tags from underlying chapters for a Series
    /// </summary>
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
    public AgeRating AgeRating { get; set; }
    public string? ReleaseDate { get; set; }
    public PublicationStatus PublicationStatus { get; set; }
    /// <summary>
    /// Summary for the Chapter/Issue
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// Language for the Chapter/Issue
    /// </summary>
    public string? Language { get; set; }
    /// <summary>
    /// Number in the TotalCount of issues
    /// </summary>
    public int Count { get; set; }
    /// <summary>
    /// Total number of issues for the series
    /// </summary>
    public int TotalCount { get; set; }
    /// <summary>
    /// Number of Words for this chapter. Only applies to Epub
    /// </summary>
    public long WordCount { get; set; }

}
