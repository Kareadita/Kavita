using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;

namespace Kavita.Models.Extensions;

public static class EncodeFormatExtensions
{
    [Obsolete("Use ForKind() instead")]
    public static string GetExtension(this EncodeFormat encodeFormat)
    {
        return encodeFormat switch
        {
            EncodeFormat.PNG => ".png",
            EncodeFormat.WEBP => ".webp",
            EncodeFormat.AVIF => ".avif",
            EncodeFormat.JPEG => ".jpg",
            _ => throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null)
        };
    }

    /// <summary>
    /// The format a <paramref name="kind"/> is written in under the server's encode setting. JPEG has no transparency, so
    /// kinds whose profile keeps it are written as PNG instead
    /// </summary>
    public static EncodeFormat ForKind(this EncodeFormat setting, ImageKind kind)
    {
        return setting == EncodeFormat.JPEG && ImageProfile.For(kind).KeepsTransparency ? EncodeFormat.PNG : setting;
    }
}
