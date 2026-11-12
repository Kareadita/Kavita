using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NetVips;
using Image = NetVips.Image;

namespace Kavita.Services;

public class ImageService(ILogger<ImageService> logger, IDirectoryService directoryService, IImagePipeline imagePipeline)
    : IImageService
{
    public const string ChapterCoverImageRegex = @"v\d+_c\d+";
    public const string SeriesCoverImageRegex = @"series\d+";
    public const string CollectionTagCoverImageRegex = @"tag\d+";
    public const string ReadingListCoverImageRegex = @"readinglist\d+";
    public const string PersonCoverImageRegex = @"person\d+";

    // Fixed seed for generation of ColorScapes. Do not change
    private const int ColorScapeSeed = 7;


    public void ExtractImages(string? fileFilePath, string targetDirectory, int fileCount = 1)
    {
        if (string.IsNullOrEmpty(fileFilePath)) return;
        directoryService.ExistOrCreate(targetDirectory);
        if (fileCount == 1)
        {
            directoryService.CopyFileToDirectory(fileFilePath, targetDirectory);
        }
        else
        {
            directoryService.CopyDirectoryToDirectory(directoryService.FileSystem.Path.GetDirectoryName(fileFilePath), targetDirectory,
                Parser.ImageFileExtensions);
        }
    }

    public string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        try
        {
            return WriteCover(new ImageSource.FromFile(path), fileName, outputDirectory, encodeFormat, size);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
        }

        return string.Empty;
    }

    /// <summary>
    /// Creates a thumbnail out of a stream and saves it to <paramref name="outputDirectory"/> with the passed
    /// fileName and the appropriate extension.
    /// </summary>
    /// <param name="stream">Stream to read; rewound first when it can seek</param>
    /// <param name="fileName">filename to save as without extension</param>
    /// <param name="outputDirectory">Where to output the file</param>
    /// <param name="encodeFormat">Export the file as the passed encoding</param>
    /// <param name="size">Cover size to scale and crop to</param>
    /// <returns>File name with extension of the file</returns>
    public string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        return WriteCover(new ImageSource.FromStream(stream), fileName, outputDirectory, encodeFormat, size);
    }

    public string WriteCoverThumbnail(string sourceFile, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        return WriteCover(new ImageSource.FromFile(sourceFile), fileName, outputDirectory, encodeFormat, size);
    }

    private string WriteCover(ImageSource source, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size)
    {
        return imagePipeline.Write(source, ImageKind.ChapterCover, new ImagePolicy(encodeFormat, size), outputDirectory,
            fileName + encodeFormat.GetExtension()).FileName;
    }

    public Task<string> ConvertToEncodingFormat(string filePath, string outputPath, EncodeFormat encodeFormat,
        CancellationToken ct = default)
    {
        var file = directoryService.FileSystem.FileInfo.New(filePath);
        var fileName = file.Name.Replace(file.Extension, string.Empty);
        var outputFile = Path.Join(outputPath, fileName + encodeFormat.GetExtension());

        using var sourceImage = Image.NewFromFile(filePath, false, Enums.Access.SequentialUnbuffered);
        sourceImage.WriteToFile(outputFile);
        return Task.FromResult(outputFile);
    }

    public string? GetImageExtension(string filePath)
    {
        try
        {
            using var image = Image.NewFromFile(filePath, access: Enums.Access.Sequential);
            return image.Get("vips-loader") switch
            {
                "pngload" => ".png",
                "jpegload" => ".jpg",
                "webpload" => ".webp",
                "gifload" => ".gif",
                // heifload also opens HEIC, which browsers can't show
                "heifload" when image.Contains("heif-compression") && (string) image.Get("heif-compression") == "av1" => ".avif",
                _ => null,
            };
        }
        catch (VipsException)
        {
            return null;
        }
    }



    private static (Vector3?, Vector3?) GetPrimarySecondaryColors(string imagePath)
    {
        using var image = Image.NewFromFile(imagePath);
        // Resize the image to speed up processing
        var resizedImage = image.Resize(0.1);

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
        var centroids = points.OrderBy(x => random.Next()).Take(k).ToList();

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



    /// <inheritdoc />
    public string CreateThumbnailFromBase64(string encodedImage, string fileName, ImageKind kind, ImagePolicy policy,
        string? targetDirectory = null)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encodedImage);
        }
        catch (FormatException e)
        {
            throw new KavitaException("Invalid Base64 string", e);
        }

        try
        {
            return WriteThumbnail(new ImageSource.FromBytes(bytes), fileName, kind, policy, targetDirectory);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thumbnail from base64");
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public string CreateThumbnailFromFile(string sourceFile, string fileName, ImageKind kind, ImagePolicy policy,
        string? targetDirectory = null)
    {
        try
        {
            return WriteThumbnail(new ImageSource.FromFile(sourceFile), fileName, kind, policy, targetDirectory);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thumbnail from file {SourceFile}", sourceFile);
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public async Task<string> CreateThumbnailFromUrl(string url, string fileName, ImageKind kind, ImagePolicy policy)
    {
        var downloadPath = directoryService.FileSystem.Path.Join(directoryService.TempDirectory, $"download_{Guid.NewGuid():N}.tmp");
        try
        {
            await FlurlConfiguration.DownloadToFileAsync(url, downloadPath, ControllerConstants.MaxUploadSizeBytes);

            return WriteThumbnail(new ImageSource.FromFile(downloadPath), fileName, kind, policy, null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thumbnail from url");
        }
        finally
        {
            directoryService.DeleteFiles([downloadPath]);
        }

        return string.Empty;
    }

    private string WriteThumbnail(ImageSource source, string fileName, ImageKind kind, ImagePolicy policy, string? targetDirectory)
    {
        return imagePipeline.Write(source, kind, policy, targetDirectory ?? directoryService.CoverImageDirectory,
            fileName + policy.Format.GetExtension()).FileName;
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
        return $"series{seriesId}"; // If this ever changes, also needs to update in SeriesRepository#GetAllWithCoversInDifferentEncodingAsync
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
        return $"{new Uri(url).Host.Replace("www.", string.Empty)}{encodeFormat.GetExtension()}";
    }

    public static string GetPublisherFormat(string publisher, EncodeFormat encodeFormat)
    {
        return $"{publisher}{encodeFormat.GetExtension()}";
    }


    public static void CreateMergedImage(IList<string> coverImages, CoverImageSize size, string dest)
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


        var image = Image.Black(width, height);

        var thumbnailWidth = image.Width / cols;
        var thumbnailHeight = image.Height / rows;

        for (var i = 0; i < coverImages.Count; i++)
        {
            if (!File.Exists(coverImages[i])) continue;
            var tile = Image.NewFromFile(coverImages[i], access: Enums.Access.Sequential);
            tile = tile.ThumbnailImage(thumbnailWidth, height: thumbnailHeight);

            var row = i / cols;
            var col = i % cols;

            var x = col * thumbnailWidth;
            var y = row * thumbnailHeight;

            if (coverImages.Count == 3 && i == 2)
            {
                x = (image.Width - thumbnailWidth) / 2;
                y = thumbnailHeight;
            }

            image = image.Insert(tile, x, y);
        }

        image.WriteToFile(dest);
    }

    public void UpdateColorScape(IHasCoverImage entity)
    {
        var colors = CalculateColorScape(
            directoryService.FileSystem.Path.Join(directoryService.CoverImageDirectory, entity.CoverImage));
        entity.PrimaryColor = colors.Primary;
        entity.SecondaryColor = colors.Secondary;
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
