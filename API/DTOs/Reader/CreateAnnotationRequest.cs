using API.Entities.Enums;

namespace API.DTOs.Reader;
#nullable enable

public sealed record CreateAnnotationRequest
{
    public int Id { get; set; }
    /// <summary>
    /// Starting point of the Highlight
    /// </summary>
    public required string XPath { get; set; }
    /// <summary>
    /// Ending point of the Highlight. Can be the same as <see cref="XPath"/>
    /// </summary>
    public string? EndingXPath { get; set; }

    /// <summary>
    /// The text selected.
    /// </summary>
    public string? SelectedText { get; set; }
    /// <summary>
    /// Rich text Comment
    /// </summary>
    public string? Comment { get; set; }
    /// <summary>
    /// The number of characters selected
    /// </summary>
    public int HighlightCount { get; set; }

    /// <summary>
    /// Selected Highlight Slot Index [0-4]
    /// </summary>
    public int SelectedSlotIndex { get; set; }
    public bool ContainsSpoiler { get; set; }
    public int PageNumber { get; set; }

    public required int ChapterId { get; set; }
    public required int VolumeId { get; set; }
    public required int SeriesId { get; set; }
}
