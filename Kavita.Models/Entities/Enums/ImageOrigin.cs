using System.ComponentModel;

namespace Kavita.Models.Entities.Enums;

/// <summary>
/// Where the image in an <see cref="ImageFile"/> came from
/// </summary>
public enum ImageOrigin
{
    /// <summary>
    /// Extracted from the file during a scan
    /// </summary>
    [Description("Scan")]
    Scan = 0,
    /// <summary>
    /// Uploaded by a user, from a file or a URL
    /// </summary>
    [Description("Upload")]
    Upload = 1,
    [Description("Kavita+")]
    KavitaPlus = 2,
    [Description("CoversDB")]
    CoversDb = 3,
    /// <summary>
    /// A user's picture from their OIDC provider
    /// </summary>
    [Description("OIDC")]
    Oidc = 4,
    /// <summary>
    /// Built by Kavita from item covers, such as a reading list or collection cover
    /// </summary>
    [Description("Composite")]
    Composite = 5,
    /// <summary>
    /// Written before Kavita recorded where images came from
    /// </summary>
    [Description("Legacy")]
    Legacy = 6,
}
