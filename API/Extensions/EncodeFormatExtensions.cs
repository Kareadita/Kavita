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
            EncodeFormat.JPEG => ".jpg",
            _ => throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null)
        };
    }

    public static int DefaultQuality(this EncodeFormat encodeFormat)
    {
        return encodeFormat switch
        {
            EncodeFormat.PNG => 100, // (Image Magick Maximum Deflate Compression) (In case of PNG, png is always lossless, Quality indicate the compression level)
            EncodeFormat.WEBP => 100,
            EncodeFormat.AVIF => 100,
            EncodeFormat.JPEG => 99, // (Best Compression speed, with almost no visual quality loss)
            _ => throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null)
        };
    }
}
