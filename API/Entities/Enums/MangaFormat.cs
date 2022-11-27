using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents the format of the file
/// </summary>
public enum MangaFormat
{
    /// <summary>
    /// Image file
    /// See <see cref="Services.Tasks.Scanner.Parser.Parser.ImageFileExtensions"/> for supported extensions
    /// </summary>
    [Description("Image")]
    Image = 0,
    /// <summary>
    /// Archive based file
    /// See <see cref="Services.Tasks.Scanner.Parser.Parser.ArchiveFileExtensions"/> for supported extensions
    /// </summary>
    [Description("Archive")]
    Archive = 1,
    /// <summary>
    /// Unknown
    /// </summary>
    /// <remarks>Default state for all files, but at end of processing, will never be Unknown.</remarks>
    [Description("Unknown")]
    Unknown = 2,
    /// <summary>
    /// EPUB File
    /// </summary>
    [Description("EPUB")]
    Epub = 3,
    /// <summary>
    /// PDF File
    /// </summary>
    [Description("PDF")]
    Pdf = 4
}
