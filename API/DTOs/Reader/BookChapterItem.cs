using System.Collections.Generic;

namespace API.DTOs.Reader;

public class BookChapterItem
{
    /// <summary>
    /// Name of the Chapter
    /// </summary>
    public string Title { get; set; } = default!;
    /// <summary>
    /// A part represents the id of the anchor so we can scroll to it. 01_values.xhtml#h_sVZPaxUSy/
    /// </summary>
    public string Part { get; set; } = default!;
    /// <summary>
    /// Page Number to load for the chapter
    /// </summary>
    public int Page { get; set; }
    public ICollection<BookChapterItem> Children { get; set; } = default!;
}
