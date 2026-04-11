namespace Kavita.Models.DTOs.Filtering.v2.FilterFields;

public enum AnnotationFilterField
{
    Owner = 1,
    Library = 2,
    Spoiler = 3,
    /// <summary>
    /// When used, only returns your own annotations
    /// </summary>
    HighlightSlot = 4,
    /// <summary>
    /// This is the text selected in the book
    /// </summary>
    Selection = 5,
    /// <summary>
    /// This is the text the user wrote
    /// </summary>
    Comment = 6,
    Series = 7,
    Likes = 8,
    LikedBy = 9,
}
