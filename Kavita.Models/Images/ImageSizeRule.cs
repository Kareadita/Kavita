using System.ComponentModel;

namespace Kavita.Models.Images;

public enum ImageSizeRule
{
    /// <summary>
    /// Scaled and smart-cropped to the server's cover size
    /// </summary>
    [Description("Cover Size")]
    CoverSize = 0,
    /// <summary>
    /// Shrunk to fit inside the profile's box, keeping the aspect ratio, never upscaled
    /// </summary>
    [Description("Fit Within")]
    FitWithin = 1,
    /// <summary>
    /// Kept at the source size, only re-encoded
    /// </summary>
    [Description("Original")]
    Original = 2,
}
