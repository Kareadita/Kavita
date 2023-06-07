using System.ComponentModel;

namespace API.Entities.Enums;

public enum EncodeFormat
{
    [Description("PNG")]
    PNG = 0,
    [Description("WebP")]
    WEBP = 1,
    [Description("AVIF")]
    AVIF = 2
}
