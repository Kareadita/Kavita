using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents the writing styles for the book-reader
/// </summary>
public enum WritingStyle
{
    /// <summary>
    /// Vertical writing style for the book-reader
    /// </summary>
    [Description ("Vertical")]
    Vertical = 0,
    /// <summary>
    /// Horizontal writing style for the book-reader
    /// </summary>
    [Description ("Horizontal")]
    Horizontal = 1
}
