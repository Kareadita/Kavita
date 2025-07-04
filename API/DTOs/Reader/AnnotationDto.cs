using System;
using API.Entities;
using API.Entities.Enums;

namespace API.DTOs.Reader;

/// <summary>
/// Represents an annotation on a book
/// </summary>
public sealed record AnnotationDto
{
    public int Id { get; set; }
    /// <summary>
    /// Starting point of the Highlight
    /// </summary>
    public required string XPath { get; set; }
    /// <summary>
    /// Ending point of the Highlight. Can be the same as <see cref="XPath"/>
    /// </summary>
    public string EndingXPath { get; set; }

    /// <summary>
    /// The text selected.
    /// </summary>
    public string SelectedText { get; set; }
    /// <summary>
    /// Rich text Comment
    /// </summary>
    public string? Comment { get; set; }
    /// <summary>
    /// The number of characters selected
    /// </summary>
    public int HighlightCount { get; set; }
    public bool ContainsSpoiler { get; set; }
    public int PageNumber { get; set; }

    public HightlightColor HighlightColor { get; set; }

    public required int ChapterId { get; set; }

    public required int OwnerUserId { get; set; }
    public string OwnerUsername { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
