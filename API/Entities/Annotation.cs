using System;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// Represents an annotation in the Epub reader
/// </summary>
public class Annotation : IEntityDate
{
    public int Id { get; set; }
    /// <summary>
    /// Starting point of the Highlight
    /// </summary>
    public required string XPath { get; set; }
    /// <summary>
    /// Ending point of the Hightlight. Can be the same as <see cref="XPath"/>
    /// </summary>
    public string EndingXPath { get; set; }

    /// <summary>
    /// The text selected.
    /// </summary>
    public string SelectedText { get; set; }
    /// <summary>
    /// The number of characters selected
    /// </summary>
    public int HighlightCount { get; set; }

    public HightlightColor HightlightColor { get; set; }

    public required int SeriesId { get; set; }
    public required int VolumeId { get; set; }
    public required int ChapterId { get; set; }
    public Chapter Chapter { get; set; }

    public required int AppUserId { get; set; }
    public AppUser AppUser { get; set; }

    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
