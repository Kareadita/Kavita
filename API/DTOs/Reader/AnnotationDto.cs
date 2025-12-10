using System;
using System.Collections.Generic;
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
    /// <inheritdoc cref="AppUserAnnotation.CommentHtml"/>
    public string? CommentHtml { get; set; }
    /// <inheritdoc cref="AppUserAnnotation.CommentPlainText"/>
    public string? CommentPlainText { get; set; }
    /// <summary>
    /// Title of the TOC Chapter within Epub (not Chapter Entity)
    /// </summary>
    public string? ChapterTitle { get; set; }
    /// <summary>
    /// A calculated selection of the surrounding text. This does not update after creation.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// The number of characters selected
    /// </summary>
    public int HighlightCount { get; set; }
    public bool ContainsSpoiler { get; set; }
    public int PageNumber { get; set; }

    /// <summary>
    /// Selected Highlight Slot Index [0-4]
    /// </summary>
    public int SelectedSlotIndex { get; set; }

    /// <inheritdoc cref="AppUserAnnotation.Likes"/>
    public IList<int> Likes { get; set; }

    public string SeriesName { get; set; }
    public string LibraryName { get; set; }


    public required int ChapterId { get; set; }
    public required int VolumeId { get; set; }
    public required int SeriesId { get; set; }
    public required int LibraryId { get; set; }

    public required int OwnerUserId { get; set; }
    public string OwnerUsername { get; set; }
    /// <summary>
    /// The age rating of the series this annotation is linked to
    /// </summary>
    /// <remarks>Not required when creating/updating an annotation, this is added in flight</remarks>
    public AgeRating AgeRating { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
