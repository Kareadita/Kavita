using System;
using System.Collections.Generic;
using API.DTOs.Person;

namespace API.DTOs.SeriesDetail;

#nullable enable

public sealed record UserReviewExtendedDto
{
    public int Id { get; set; }
    /// <summary>
    /// The main review
    /// </summary>
    public string Body { get; set; }
    /// <summary>
    /// The series this is for
    /// </summary>
    public int SeriesId { get; set; }
    public int? ChapterId { get; set; }
    /// <summary>
    /// The library this series belongs in
    /// </summary>
    public int LibraryId { get; set; }
    /// <summary>
    /// The user who wrote this
    /// </summary>
    public string Username { get; set; }
    public float Rating { get; set; }
    public SeriesDto Series { get; set; }
    public ChapterDto? Chapter { get; set; }
    public DateTime CreatedUtc { get; set; }

    public ICollection<PersonDto> Writers { get; set; } = [];
}
