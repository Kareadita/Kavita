using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using NetVips;
using Image = NetVips.Image;

namespace Kavita.Services.Helpers;

public static class ImageHelper
{
    // Fixed seed for generation of ColorScapes. Do not change
    private const int ColorScapeSeed = 7;

    /// <summary>
    /// Reads the image format from the file header, never the file name
    /// </summary>
    /// <returns>The extension with its dot (<c>.png</c>, <c>.jpg</c>, <c>.webp</c>, <c>.gif</c>, <c>.avif</c>), or null
    /// when the file is not an image or is a format Kavita does not take for covers</returns>
    public static string? GetImageExtension(string filePath)
    {
        try
        {
            using var image = Image.NewFromFile(filePath, access: Enums.Access.Sequential);
            return GetExtension(image);
        }
        catch (VipsException)
        {
            return null;
        }
    }

    /// <summary>
    /// <see cref="GetImageExtension"/> and the size, read from the header of an image already in memory
    /// </summary>
    /// <returns>Null when the bytes are not an image or are a format Kavita does not take for covers</returns>
    public static ImageHeader? GetImageHeader(byte[] bytes)
    {
        try
        {
            using var image = Image.NewFromBuffer(bytes, access: Enums.Access.Sequential);
            var extension = GetExtension(image);
            return extension == null ? null : new ImageHeader(extension, image.Width, image.Height);
        }
        catch (VipsException)
        {
            return null;
        }
    }

    private static string? GetExtension(Image image)
    {
        // Loading from file reports pngload, while from memory reports pngload_buffer
        return (string) image.Get("vips-loader") switch
        {
            "pngload" or "pngload_buffer" => ".png",
            "jpegload" or "jpegload_buffer" => ".jpg",
            "webpload" or "webpload_buffer" => ".webp",
            "gifload" or "gifload_buffer" => ".gif",
            // heifload also opens HEIC, which browsers can't show
            "heifload" or "heifload_buffer" when image.Contains("heif-compression") && (string) image.Get("heif-compression") == "av1" => ".avif",
            _ => null,
        };
    }



    private static (Vector3?, Vector3?) GetPrimarySecondaryColors(string imagePath)
    {
        using var image = Image.NewFromFile(imagePath);
        // Resize the image to speed up processing
        using var resizedImage = image.Resize(0.1);

        // Convert image to RGB array
        var pixels = resizedImage.WriteToMemory<byte>().ToArray();

        // Convert to list of Vector3 (RGB)
        var rgbPixels = new List<Vector3>();
        for (var i = 0; i < pixels.Length - 2; i += 3)
        {
            rgbPixels.Add(new Vector3(pixels[i], pixels[i + 1], pixels[i + 2]));
        }

        // Perform k-means clustering
        var clusters = KMeansClustering(rgbPixels, 4);

        var sorted = SortByVibrancy(clusters);

        // Ensure white and black are not selected as primary/secondary colors
        sorted = sorted.Where(c => !IsCloseToWhiteOrBlack(c)).ToList();

        if (sorted.Count >= 2)
        {
            return (sorted[0], sorted[1]);
        }
        if (sorted.Count == 1)
        {
            return (sorted[0], null);
        }

        return (null, null);
    }

    private static List<Vector3> KMeansClustering(List<Vector3> points, int k, int maxIterations = 100)
    {
        // This is non-security context, ignore warning
#pragma warning disable S2245
        var random = new Random(ColorScapeSeed);
#pragma warning restore S2245
        var centroids = points.OrderBy(_ => random.Next()).Take(k).ToList();

        for (var i = 0; i < maxIterations; i++)
        {
            var clusters = new List<Vector3>[k];
            for (var j = 0; j < k; j++)
            {
                clusters[j] = [];
            }

            foreach (var point in points)
            {
                var nearestCentroidIndex = centroids
                    .Select((centroid, index) => new { Index = index, Distance = Vector3.DistanceSquared(centroid, point) })
                    .OrderBy(x => x.Distance)
                    .First().Index;
                clusters[nearestCentroidIndex].Add(point);
            }

            var newCentroids = clusters.Select(cluster =>
                cluster.Count != 0 ? new Vector3(
                    cluster.Average(p => p.X),
                    cluster.Average(p => p.Y),
                    cluster.Average(p => p.Z)
                ) : Vector3.Zero
            ).ToList();

            if (centroids.SequenceEqual(newCentroids))
                break;

            centroids = newCentroids;
        }

        return centroids;
    }


    private static List<Vector3> SortByVibrancy(List<Vector3> colors)
    {
        return colors.OrderByDescending(c =>
        {
            var max = Math.Max(c.X, Math.Max(c.Y, c.Z));
            var min = Math.Min(c.X, Math.Min(c.Y, c.Z));
            return (max - min) / max;
        }).ToList();
    }

    private static bool IsCloseToWhiteOrBlack(Vector3 color)
    {
        const int threshold = 30;
        return color is {X: > 255 - threshold, Y: > 255 - threshold, Z: > 255 - threshold} ||
               color is {X: < threshold, Y: < threshold, Z: < threshold};
    }

    private static string RgbToHex(Vector3 color)
    {
        return $"#{(int)color.X:X2}{(int)color.Y:X2}{(int)color.Z:X2}";
    }

    /// <summary>
    /// Generates the Primary and Secondary colors from a file
    /// </summary>
    /// <remarks>This may use a second most common color or a complementary color. It's up to implemenation to choose what's best</remarks>
    /// <param name="sourceFile"></param>
    /// <returns></returns>
    public static ColorScape CalculateColorScape(string sourceFile)
    {
        if (!File.Exists(sourceFile)) return new ColorScape() {Primary = null, Secondary = null};

        var colors = GetPrimarySecondaryColors(sourceFile);

        return new ColorScape()
        {
            Primary = colors.Item1 == null ? null : RgbToHex(colors.Item1.Value),
            Secondary = colors.Item2 == null ? null : RgbToHex(colors.Item2.Value)
        };
    }




    /// <summary>
    /// Returns the name format for a chapter cover image
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public static string GetChapterFormat(int chapterId, int volumeId)
    {
        return $"v{volumeId}_c{chapterId}";
    }

    /// <summary>
    /// Returns the name format for a volume cover image (custom)
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public static string GetVolumeFormat(int volumeId)
    {
        return $"v{volumeId}";
    }

    public static bool IsChapterFormatForVolume(string filePath, int volumeId)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.StartsWith($"v{volumeId}_c", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the name format for a library cover image
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    public static string GetLibraryFormat(int libraryId)
    {
        return $"l{libraryId}";
    }

    /// <summary>
    /// Returns the name format for a series cover image
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public static string GetSeriesFormat(int seriesId)
    {
        return $"series{seriesId}";
    }

    /// <summary>
    /// Returns the name format for a collection tag cover image
    /// </summary>
    /// <param name="tagId"></param>
    /// <returns></returns>
    public static string GetCollectionTagFormat(int tagId)
    {
        return $"tag{tagId}";
    }

    /// <summary>
    /// Returns the name format for a reading list cover image
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    public static string GetReadingListFormat(int readingListId)
    {
        // ReSharper disable once StringLiteralTypo
        return $"readinglist{readingListId}";
    }

    /// <summary>
    /// Returns the name format for a thumbnail (temp thumbnail)
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    public static string GetThumbnailFormat(int chapterId)
    {
        return $"thumbnail{chapterId}";
    }

    /// <summary>
    /// Returns the name format for a person cover
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    public static string GetPersonFormat(int personId)
    {
        return $"person{personId}";
    }

    /// <summary>
    /// Returns the name format for a user cover
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static string GetUserFormat(int userId)
    {
        return $"user{userId}";
    }

    public static string GetWebLinkFormat(string url, EncodeFormat encodeFormat)
    {
        return $"{new Uri(url).Host.Replace("www.", string.Empty)}{encodeFormat.ForKind(ImageKind.Favicon).GetExtension()}";
    }

    public static string GetPublisherFormat(string publisher, EncodeFormat encodeFormat)
    {
        return $"{publisher}{encodeFormat.ForKind(ImageKind.Publisher).GetExtension()}";
    }


    /// <summary>
    /// Tiles up to 4 covers into one image, sized to <paramref name="size"/>
    /// </summary>
    /// <returns>Lossless PNG bytes, for the pipeline to encode</returns>
    public static byte[] CreateMergedImageBuffer(IList<string> coverImages, CoverImageSize size)
    {
        using var image = BuildMergedImage(coverImages, size);
        return image.PngsaveBuffer(compression: 1);
    }

    private static Image BuildMergedImage(IList<string> coverImages, CoverImageSize size)
    {
        var (width, height) = size.GetDimensions();
        int rows, cols;

        if (coverImages.Count == 1)
        {
            rows = 1;
            cols = 1;
        }
        else if (coverImages.Count == 2)
        {
            rows = 1;
            cols = 2;
        }
        else
        {
            rows = 2;
            cols = 2;
        }


        var image = Image.Black(width, height, bands: 3);
        try
        {
            var thumbnailWidth = image.Width / cols;
            var thumbnailHeight = image.Height / rows;

            for (var i = 0; i < coverImages.Count; i++)
            {
                if (!File.Exists(coverImages[i])) continue;
                using var source = Image.NewFromFile(coverImages[i], access: Enums.Access.Sequential);
                using var thumbnail = source.ThumbnailImage(thumbnailWidth, height: thumbnailHeight);
                // Insert needs equal band counts (or a single-band side), so an RGBA cover next to RGB ones throws
                using var tile = thumbnail.HasAlpha() ? thumbnail.Flatten() : thumbnail.Copy();

                var row = i / cols;
                var col = i % cols;

                var x = col * thumbnailWidth;
                var y = row * thumbnailHeight;

                if (coverImages.Count == 3 && i == 2)
                {
                    x = (image.Width - thumbnailWidth) / 2;
                    y = thumbnailHeight;
                }

                var merged = image.Insert(tile, x, y);
                image.Dispose();
                image = merged;
            }

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public static (int R, int G, int B) HexToRgb(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) throw new ArgumentException("Hex cannot be null");

        // Remove the leading '#' if present
        hex = hex.TrimStart('#');

        // Ensure the hex string is valid
        if (hex.Length != 6 && hex.Length != 3)
        {
            throw new ArgumentException("Hex string should be 6 or 3 characters long.");
        }

        if (hex.Length == 3)
        {
            // Expand shorthand notation to full form (e.g., "abc" -> "aabbcc")
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        }

        // Parse the hex string into RGB components
        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);

        return (r, g, b);
    }


}
