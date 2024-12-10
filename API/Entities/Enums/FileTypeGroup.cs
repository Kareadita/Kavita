using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents a set of file types that can be scanned
/// </summary>
public enum FileTypeGroup
{
    [Description("Archive")]
    Archive = 1,
    [Description("EPub")]
    Epub = 2,
    [Description("Pdf")]
    Pdf = 3,
    [Description("Images")]
    Images = 4
}
