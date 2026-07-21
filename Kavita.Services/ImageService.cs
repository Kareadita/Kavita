using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NetVips;
using Image = NetVips.Image;

namespace Kavita.Services;

public class ImageService(ILogger<ImageService> logger, IDirectoryService directoryService)
    : IImageService
{
    public const string Name = "ImageService";

    public const string ChapterCoverImageRegex = @"v\d+_c\d+";
    public const string SeriesCoverImageRegex = @"series\d+";
    public const string CollectionTagCoverImageRegex = @"tag\d+";
    public const string ReadingListCoverImageRegex = @"readinglist\d+";
    public const string PersonCoverImageRegex = @"person\d+";

    /// <summary>
    /// Width of the Thumbnail generation
    /// </summary>
    private const int ThumbnailWidth = 320;
    /// <summary>
    /// Height of the Thumbnail generation
    /// </summary>
    private const int ThumbnailHeight = 455;
    /// <summary>
    /// Width of a cover for Library
    /// </summary>
    public const int LibraryThumbnailWidth = 32;


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

    /// <summary>
    /// Tries to determine if there is a better mode for resizing
    /// </summary>
    /// <param name="image"></param>
    /// <param name="targetWidth"></param>
    /// <param name="targetHeight"></param>
    /// <returns></returns>
    public static Enums.Size GetSizeForDimensions(Image image, int targetWidth, int targetHeight)
    {
        try
        {
            if (WillScaleWell(image, targetWidth, targetHeight) || IsLikelyWideImage(image.Width, image.Height))
            {
                return Enums.Size.Force;
            }
        }
        catch (Exception)
        {
            /* Swallow */
        }

        return Enums.Size.Both;
    }

    public static Enums.Interesting? GetCropForDimensions(Image image, int targetWidth, int targetHeight)
    {
        try
        {
            if (WillScaleWell(image, targetWidth, targetHeight) || IsLikelyWideImage(image.Width, image.Height))
            {
                return null;
            }
        } catch (Exception)
        {
            /* Swallow */
            return null;
        }

        return Enums.Interesting.Attention;
    }

    public static bool WillScaleWell(Image sourceImage, int targetWidth, int targetHeight, double tolerance = 0.1)
    {
        // Calculate the aspect ratios
        var sourceAspectRatio = (double) sourceImage.Width / sourceImage.Height;
        var targetAspectRatio = (double) targetWidth / targetHeight;

        // Compare aspect ratios
        if (Math.Abs(sourceAspectRatio - targetAspectRatio) > tolerance)
        {
            return false; // Aspect ratios differ significantly
        }

        // Calculate scaling factors
        var widthScaleFactor = (double) targetWidth / sourceImage.Width;
        var heightScaleFactor = (double) targetHeight / sourceImage.Height;

        // Check resolution quality (example thresholds)
        if (widthScaleFactor > 2.0 || heightScaleFactor > 2.0)
        {
            return false; // Scaling factor too large
        }

        return true; // Image will scale well
    }

    private static bool IsLikelyWideImage(int width, int height)
    {
        var aspectRatio = (double) width / height;
        return aspectRatio > 1.25;
    }

    public string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        try
        {
            var (width, height) = size.GetDimensions();
            using var sourceImage = Image.NewFromFile(path, false, Enums.Access.SequentialUnbuffered);

            using var thumbnail = Image.Thumbnail(path, width, height: height,
                size: GetSizeForDimensions(sourceImage, width, height),
                crop: GetCropForDimensions(sourceImage, width, height));
            var filename = fileName + encodeFormat.GetExtension();
            thumbnail.WriteToFile(directoryService.FileSystem.Path.Join(outputDirectory, filename));
            return filename;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
        }

        return string.Empty;
    }

    /// <summary>
    /// Creates a thumbnail out of a memory stream and saves to <see cref="DirectoryService.CoverImageDirectory"/> with the passed
    /// fileName and the appropriate extension.
    /// </summary>
    /// <param name="stream">Stream to write to disk. Ensure this is rewinded.</param>
    /// <param name="fileName">filename to save as without extension</param>
    /// <param name="outputDirectory">Where to output the file, defaults to covers directory</param>
    /// <param name="encodeFormat">Export the file as the passed encoding</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    public string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        var (targetWidth, targetHeight) = size.GetDimensions();
        if (stream.CanSeek) stream.Position = 0;
        using var sourceImage = Image.NewFromStream(stream);

        var scalingSize = GetSizeForDimensions(sourceImage, targetWidth, targetHeight);
        var scalingCrop = GetCropForDimensions(sourceImage, targetWidth, targetHeight);

        using var thumbnail = sourceImage.ThumbnailImage(targetWidth, targetHeight,
            size: scalingSize,
            crop: scalingCrop);

        var filename = fileName + encodeFormat.GetExtension();
        directoryService.ExistOrCreate(outputDirectory);

        try
        {
            directoryService.FileSystem.File.Delete(directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}

        try
        {
            thumbnail.WriteToFile(directoryService.FileSystem.Path.Join(outputDirectory, filename));

            return filename;
        }
        catch (VipsException)
        {
            // NetVips Issue: https://github.com/kleisauke/net-vips/issues/234
            // Saving pdf covers from a stream can fail, so revert to old code

            if (stream.CanSeek) stream.Position = 0;
            using var thumbnail2 = Image.ThumbnailStream(stream, targetWidth, height: targetHeight,
                size: scalingSize,
                crop: scalingCrop);
            thumbnail2.WriteToFile(directoryService.FileSystem.Path.Join(outputDirectory, filename));

            return filename;
        }
    }

    public string WriteCoverThumbnail(string sourceFile, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        var (width, height) = size.GetDimensions();
        using var sourceImage = Image.NewFromFile(sourceFile, false, Enums.Access.SequentialUnbuffered);

        using var thumbnail = Image.Thumbnail(sourceFile, width, height: height,
            size: GetSizeForDimensions(sourceImage, width, height),
            crop: GetCropForDimensions(sourceImage, width, height));
        var filename = fileName + encodeFormat.GetExtension();
        directoryService.ExistOrCreate(outputDirectory);
        try
        {
            directoryService.FileSystem.File.Delete(directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}
        thumbnail.WriteToFile(directoryService.FileSystem.Path.Join(outputDirectory, filename));
        return filename;
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

    public Task<bool> IsImage(string filePath, CancellationToken ct = default)
    {
        try
        {
            // NetVips resolves the loader from the file header without decoding the pixels; if the
            // file is not a recognized image, NewFromFile throws a VipsException.
            ct.ThrowIfCancellationRequested();
            using var image = Image.NewFromFile(filePath, access: Enums.Access.Sequential);
            return Task.FromResult(true);
        }
        catch (OperationCanceledException)
        {
            // This allows cancellation to propagate upwards
            throw;
        }
        catch (Exception)
        {
            /* Swallow Exception */
        }

        return Task.FromResult(false);
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

    private static Dictionary<Vector3, int> GenerateColorHistogram(Image image)
    {
        var pixels = image.WriteToMemory().ToArray();
        var histogram = new Dictionary<Vector3, int>();

        for (var i = 0; i < pixels.Length; i += 3)
        {
            var color = new Vector3(pixels[i], pixels[i + 1], pixels[i + 2]);
            if (!histogram.TryAdd(color, 1))
            {
                histogram[color]++;
            }
        }

        return histogram;
    }

    private static List<Vector3> KMeansClustering(List<Vector3> points, int k, int maxIterations = 100)
    {
        var random = new Random();
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
    public string CreateThumbnailFromBase64(string encodedImage, string fileName, EncodeFormat encodeFormat,
        int thumbnailWidth = ThumbnailWidth, int thumbnailHeight = ThumbnailHeight, string? targetDirectory = null)
    {
        // TODO: This code has no concept of cropping nor Thumbnail Size
        try
        {
            targetDirectory ??= directoryService.CoverImageDirectory;
            using var thumbnail = Image.ThumbnailBuffer(Convert.FromBase64String(encodedImage), thumbnailWidth, height: thumbnailHeight);

            fileName += encodeFormat.GetExtension();
            thumbnail.WriteToFile(directoryService.FileSystem.Path.Join(targetDirectory, fileName));

            return fileName;
        }
        catch (FormatException e)
        {
            throw new KavitaException("Invalid Base64 string", e);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thumbnail from url");
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public string CreateThumbnailFromFile(string sourceFile, string fileName, EncodeFormat encodeFormat,
        int thumbnailWidth = 320, int thumbnailHeight = 455, string? targetDirectory = null)
    {
        try
        {
            targetDirectory ??= directoryService.CoverImageDirectory;
            using var thumbnail = Image.Thumbnail(sourceFile, thumbnailWidth, thumbnailHeight);

            fileName += encodeFormat.GetExtension();
            thumbnail.WriteToFile(directoryService.FileSystem.Path.Join(targetDirectory, fileName));

            return fileName;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thumbnail from file {SourceFile}", sourceFile);
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public async Task<string> CreateThumbnailFromUrl(string url, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = ThumbnailWidth, int thumbnailHeight = ThumbnailHeight)
    {
        try
        {
            var imageStream = await FlurlConfiguration.CreateSafeRequest(url)
                .AllowHttpStatus("2xx,304")
                .GetStreamAsync();

            using var thumbnail = Image.ThumbnailStream(imageStream, thumbnailWidth, height: thumbnailHeight);

            fileName += encodeFormat.GetExtension();
            thumbnail.WriteToFile(directoryService.FileSystem.Path.Join(directoryService.CoverImageDirectory, fileName));

            return fileName;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating thumbnail from url");
        }

        return string.Empty;
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
