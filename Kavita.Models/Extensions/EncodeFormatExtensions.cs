using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Extensions;

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
