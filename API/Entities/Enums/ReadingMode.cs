using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents the reading modes for the book-reader
/// </summary>
public enum ReadingMode
{
    /// <summary>
    /// Vertical reading mode for the book-reader
    /// </summary>
    [Description ("Vertically")]
    Vertically = 0,
    /// <summary>
    /// Horizontal reading mode for the book-reader
    /// </summary>
    [Description ("Horizontally")]
    Horizontally = 1
}
