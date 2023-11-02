using System;
using API.Entities.Enums;

namespace API.Extensions;
#nullable enable

public static class EncodeFormatExtensions
{
    public static string GetExtension(this EncodeFormat encodeFormat)
    {
        return encodeFormat switch
        {
            EncodeFormat.PNG => ".png",
            EncodeFormat.WEBP => ".webp",
            EncodeFormat.AVIF => ".avif",
            _ => throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null)
        };
    }
}
