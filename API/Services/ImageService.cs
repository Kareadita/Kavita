﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Extensions;
using API.Services.ImageServices;
using API.Services.Tasks.Scanner.Parser;
using EasyCaching.Core;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.Common;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;


namespace API.Services;
/// <summary>
/// Interface for the ImageService.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Extracts images from a file and copies them to the target directory.
    /// </summary>
    /// <param name="fileFilePath">The file path of the source file.</param>
    /// <param name="targetDirectory">The target directory to copy the images to.</param>
    /// <param name="fileCount">The number of files to extract. Default is 1.</param>
    void ExtractImages(string fileFilePath, string targetDirectory, int fileCount = 1);

    /// <summary>
    /// Gets the cover image from the specified path and saves it to the output directory.
    /// </summary>
    /// <param name="path">The path of the image file.</param>
    /// <param name="fileName">The name of the output file.</param>
    /// <param name="outputDirectory">The output directory to save the image.</param>
    /// <param name="encodeFormat">The encoding format to convert and save the image.</param>
    /// <param name="size">The size of the cover image.</param>
    /// <param name="quality">The quality of the image. Default is 100.</param>
    /// <returns>The file name with extension of the saved image.</returns>
    string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size, int quality = 100);

    /// <summary>
    /// Creates a thumbnail version of a base64 encoded image.
    /// </summary>
    /// <param name="encodedImage">The base64 encoded image.</param>
    /// <param name="fileName">The name of the output file.</param>
    /// <param name="encodeFormat">The encoding format to convert and save the image.</param>
    /// <param name="thumbnailWidth">The width of the thumbnail. Default is 320.</param>
    /// <param name="quality">The quality of the image. Default is 100.</param>
    /// <returns>The file name with extension of the saved thumbnail image.</returns>
    string CreateThumbnailFromBase64(string encodedImage, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = 320, int quality = 100);

    /// <summary>
    /// Creates a thumbnail out of a memory stream and saves to <see cref="DirectoryService.CoverImageDirectory"/> with the passed
    /// fileName and the appropriate extension.
    /// </summary>
    /// <param name="stream">Stream to write to disk. Ensure this is rewinded.</param>
    /// <param name="fileName">filename to save as without extension</param>
    /// <param name="outputDirectory">Where to output the file, defaults to covers directory</param>
    /// <param name="encodeFormat">Export the file as the passed encoding</param>
    /// <param name="size">The size of the cover image.</param>
    /// <param name="quality">The quality of the image. Default is 100.</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default, int quality = 100);

    /// <summary>
    /// Writes out a thumbnail image from a file path input and saves to <see cref="DirectoryService.CoverImageDirectory"/> with the passed
    /// </summary>
    /// <param name="sourceFile">The path of the source image file.</param>
    /// <param name="fileName">filename to save as without extension</param>
    /// <param name="outputDirectory">Where to output the file, defaults to covers directory</param>
    /// <param name="encodeFormat">Export the file as the passed encoding</param>
    /// <param name="size">The size of the cover image.</param>
    /// <param name="quality">The quality of the image. Default is 100.</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    string WriteCoverThumbnail(string sourceFile, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default, int quality = 100);

    /// <summary>
    /// Converts the specified image file to the specified encoding format and saves it to the output path.
    /// </summary>
    /// <param name="filePath">The full path of the image file to convert.</param>
    /// <param name="outputPath">The path to save the converted image file.</param>
    /// <param name="encodeFormat">The encoding format to convert the image.</param>
    /// <param name="quality">The quality of the image. Default is 100.</param>
    /// <returns>The file path of the converted image file.</returns>
    Task<string> ConvertToEncodingFormat(string filePath, string outputPath, EncodeFormat encodeFormat, int quality = 100);

    /// <summary>
    /// Checks if the specified file is an image.
    /// </summary>
    /// <param name="filePath">The path of the file to check.</param>
    /// <returns>True if the file is an image; otherwise, false.</returns>
    Task<bool> IsImage(string filePath);

    /// <summary>
    /// Downloads the favicon from the specified URL and saves it to the output directory.
    /// </summary>
    /// <param name="url">The URL of the favicon.</param>
    /// <param name="encodeFormat">The encoding format to convert and save the favicon.</param>
    /// <param name="quality">The quality of the favicon. Default is 100.</param>
    /// <returns>The file name with extension of the saved favicon.</returns>
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat, int quality = 100);

    /// <summary>
    /// Downloads the publisher image for the specified publisher name and saves it to the output directory.
    /// </summary>
    /// <param name="publisherName">The name of the publisher.</param>
    /// <param name="encodeFormat">The encoding format to convert and save the publisher image.</param>
    /// <param name="quality">The quality of the publisher image. Default is 100.</param>
    /// <returns>The file name with extension of the saved publisher image.</returns>
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat, int quality = 100);

    /// <summary>
    /// Updates the color scape of an entity with a cover image.
    /// </summary>
    /// <param name="entity">The entity with a cover image.</param>
    void UpdateColorScape(IHasCoverImage entity);

    /// <summary>
    /// Replaces the file format of an image with the specified supported image formats by the browser, if needed, otherwise original file will be served.
    /// </summary>
    /// <param name="filename">The name of the image file.</param>
    /// <param name="supportedImageFormats">The list of supported image formats by the browser.</param>
    /// <param name="format">The encoding format to convert the image. Default is JPG</param>
    /// <param name="quality">The quality of the image. Default is 99.</param>
    /// <returns>The file name with extension of the replaced image file.</returns>
    string ReplaceImageFileFormat(string filename, List<string> supportedImageFormats = null, EncodeFormat format = EncodeFormat.JPEG, int quality = 99);

    /// <summary>
    /// Creates a merged image from a list of cover images and saves it to the specified destination.
    /// </summary>
    /// <param name="coverImages">The list of cover images.</param>
    /// <param name="size">The size of the merged image.</param>
    /// <param name="dest">The destination path to save the merged image.</param>
    /// <param name="format">The encoding format to convert and save the merged image. Default is PNG.</param>
    /// <param name="quality">The quality of the merged image. Default is 100.</param>
    void CreateMergedImage(IList<string> coverImages, CoverImageSize size, string dest, EncodeFormat format = EncodeFormat.PNG, int quality = 100);

    /// <summary>
    /// The image factory used to create and manipulate images.
    /// </summary>
    IImageFactory ImageFactory { get; }
}

public class ImageService : IImageService
{
    public const string Name = "ImageService";
    public const string ChapterCoverImageRegex = @"v\d+_c\d+";
    public const string SeriesCoverImageRegex = @"series\d+";
    public const string CollectionTagCoverImageRegex = @"tag\d+";
    public const string ReadingListCoverImageRegex = @"readinglist\d+";
    private const double WhiteThreshold = 0.95; // Colors with lightness above this are considered too close to white
    private const double BlackThreshold = 0.25; // Colors with lightness below this are considered too close to black

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

    private readonly ILogger<ImageService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory;
    private readonly IImageFactory _imageFactory;

    private static readonly string[] ValidIconRelations = {
        "icon",
        "apple-touch-icon",
        "apple-touch-icon-precomposed",
        "apple-touch-icon icon-precomposed" // ComicVine has it combined
    };

    /// <summary>
    /// A mapping of urls that need to get the icon from another url, due to strangeness (like app.plex.tv loading a black icon)
    /// </summary>
    private static readonly IDictionary<string, string> FaviconUrlMapper = new Dictionary<string, string>
    {
        ["https://app.plex.tv"] = "https://plex.tv"
    };



    private static NamedMonitor _lock = new NamedMonitor();
    /// <summary>
    /// Represents a named monitor that provides thread-safe access to objects based on their names.
    /// </summary>
    class NamedMonitor
    {
        readonly ConcurrentDictionary<string, object> _dictionary = new ConcurrentDictionary<string, object>();

        public object this[string name] => _dictionary.GetOrAdd(name, _ => new object());
    }



    /// <summary>
    /// Initializes a new instance of the <see cref="ImageService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directoryService">The directory service.</param>
    /// <param name="cacheFactory">The cache factory.</param>
    /// <param name="imageFactory">The image factory.</param>
    public ImageService(ILogger<ImageService> logger, IDirectoryService directoryService, IEasyCachingProviderFactory cacheFactory, IImageFactory imageFactory)
    {
        _logger = logger;
        _directoryService = directoryService;
        _cacheFactory = cacheFactory;
        _imageFactory = imageFactory;
    }

    public IImageFactory ImageFactory => _imageFactory;

    /// <inheritdoc/>
    public void ExtractImages(string? fileFilePath, string targetDirectory, int fileCount = 1)
    {
        if (string.IsNullOrEmpty(fileFilePath)) return;
        _directoryService.ExistOrCreate(targetDirectory);
        if (fileCount == 1)
        {
            _directoryService.CopyFileToDirectory(fileFilePath, targetDirectory);
        }
        else
        {
            _directoryService.CopyDirectoryToDirectory(_directoryService.FileSystem.Path.GetDirectoryName(fileFilePath), targetDirectory,
                Tasks.Scanner.Parser.Parser.ImageFileExtensions);
        }
    }

    /// <summary>
    /// Creates a thumbnail image from the specified image with the given width and height.
    /// If the image aspect ratio is significantly different from the target aspect ratio, it will be context aware cropped to fit.
    /// </summary>
    /// <param name="image">The image to create a thumbnail from.</param>
    /// <param name="width">The width of the thumbnail.</param>
    /// <param name="height">The height of the thumbnail.</param>
    /// <returns>The thumbnail image.</returns>
    public static IImage Thumbnail(IImage image, int width, int height)
    {
        try
        {
            if (WillScaleWell(image, width, height) || IsLikelyWideImage(image.Width, image.Height))
            {
                image.Thumbnail(width, height);
                return image;
            }
        }
        catch (Exception)
        {
            /* Swallow */
        }
        var crop = SmartCrop.Crop(image, new SmartCrop.SmartCropOptions { Width = width, Height = height });
        if (crop.TopCrop.Width != width && crop.TopCrop.Height != height)
        {
            image.Crop(crop.TopCrop.X, crop.TopCrop.Y, crop.TopCrop.Width, crop.TopCrop.Height);
        }
        image.Thumbnail(width, height);
        return image;
    }

    public static bool WillScaleWell(IImage sourceImage, int targetWidth, int targetHeight, double tolerance = 0.1)
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


    /// <inheritdoc/>
    public string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size, int quality = 100)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        try
        {
            var (width, height) = size.GetDimensions();
            using var thumbnail = Thumbnail(_imageFactory.Create(path), width, height);
            var filename = fileName + encodeFormat.GetExtension();
            thumbnail.Save(_directoryService.FileSystem.Path.Join(outputDirectory, filename), encodeFormat, quality);
            return filename;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    public string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default, int quality = 100)
    {
        var (targetWidth, targetHeight) = size.GetDimensions();
        if (stream.CanSeek) stream.Position = 0;

        using var thumbnail = Thumbnail(_imageFactory.Create(stream), targetWidth, targetHeight);
        var filename = fileName + encodeFormat.GetExtension();
        _directoryService.ExistOrCreate(outputDirectory);

        try
        {
            _directoryService.FileSystem.File.Delete(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}

        try
        {
            thumbnail.Save(_directoryService.FileSystem.Path.Join(outputDirectory, filename), encodeFormat, quality);
        }
        catch (Exception) {/* Swallow exception */}

        return filename;
    }
    /// <inheritdoc/>
    public string WriteCoverThumbnail(string sourceFile, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default, int quality = 100)
    {
        var (width, height) = size.GetDimensions();
        using var thumbnail = Thumbnail(_imageFactory.Create(sourceFile), width, height);
        var filename = fileName + encodeFormat.GetExtension();
        _directoryService.ExistOrCreate(outputDirectory);
        try
        {
            _directoryService.FileSystem.File.Delete(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}
        thumbnail.Save(_directoryService.FileSystem.Path.Join(outputDirectory, filename), encodeFormat, quality);

        return filename;
    }
    /// <inheritdoc/>
    public async Task<string> ConvertToEncodingFormat(string filePath, string outputPath, EncodeFormat encodeFormat, int quality = 100)
    {
        var file = _directoryService.FileSystem.FileInfo.New(filePath);
        var fileName = file.Name.Replace(file.Extension, string.Empty);
        var outputFile = Path.Join(outputPath, fileName + encodeFormat.GetExtension());
        using var sourceImage = _imageFactory.Create(filePath);
        await sourceImage.SaveAsync(outputFile, encodeFormat, quality).ConfigureAwait(false);

        return outputFile;
    }

    /// <inheritdoc/>
    public Task<bool> IsImage(string filePath)
    {
        try
        {
            return Task.FromResult(_imageFactory.GetDimensions(filePath) != null);
        }
        catch (Exception)
        {
            /* Swallow Exception */
        }

        return Task.FromResult(false);
    }
    /// <inheritdoc/>
    public async Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat, int quality = 100)
    {
        // Parse the URL to get the domain (including subdomain)
        var uri = new Uri(url);
        var domain = uri.Host.Replace(Environment.NewLine, string.Empty);
        var baseUrl = uri.Scheme + "://" + uri.Host;


        var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Favicon);
        var res = await provider.GetAsync<string>(baseUrl);
        if (res.HasValue)
        {
            _logger.LogInformation("Kavita has already tried to fetch from {BaseUrl} and failed. Skipping duplicate check", baseUrl);
            throw new KavitaException($"Kavita has already tried to fetch from {baseUrl} and failed. Skipping duplicate check");
        }

        await provider.SetAsync(baseUrl, string.Empty, TimeSpan.FromDays(10));
        if (FaviconUrlMapper.TryGetValue(baseUrl, out var value))
        {
            url = value;
        }

        var correctSizeLink = string.Empty;

        try
        {
            var htmlContent = url.GetStringAsync().Result;
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);
            var pngLinks = htmlDocument.DocumentNode.Descendants("link")
                .Where(link => ValidIconRelations.Contains(link.GetAttributeValue("rel", string.Empty)))
                .Select(link => link.GetAttributeValue("href", string.Empty))
                .Where(href => href.Split("?")[0].EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            correctSizeLink = (pngLinks?.Find(pngLink => pngLink.Contains("32")) ?? pngLinks?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading favicon.png for {Domain}, will try fallback methods", domain);
        }

        try
        {
            if (string.IsNullOrEmpty(correctSizeLink))
            {
                correctSizeLink = await FallbackToKavitaReaderFavicon(baseUrl);
            }
            if (string.IsNullOrEmpty(correctSizeLink))
            {
                throw new KavitaException($"Could not grab favicon from {baseUrl}");
            }

            var finalUrl = correctSizeLink;

            // If starts with //, it's coming usually from an offsite cdn
            if (correctSizeLink.StartsWith("//"))
            {
                finalUrl = "https:" + correctSizeLink;
            }
            else if (!correctSizeLink.StartsWith(uri.Scheme))
            {
                finalUrl = Url.Combine(baseUrl, correctSizeLink);
            }

            _logger.LogTrace("Fetching favicon from {Url}", finalUrl);
            // Download the favicon.ico file using Flurl
            var faviconStream = await finalUrl
                .AllowHttpStatus("2xx,304")
                .GetStreamAsync();

            // Create the destination file path
            using var image = _imageFactory.Create(faviconStream);
            var filename = ImageService.GetWebLinkFormat(baseUrl, encodeFormat);
            await image.SaveAsync(Path.Combine(_directoryService.FaviconDirectory, filename), encodeFormat, quality);
            _logger.LogDebug("Favicon for {Domain} downloaded and saved successfully", domain);
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading favicon for {Domain}", domain);
            throw;
        }
    }
    /// <inheritdoc/>
    public async Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat, int quality = 100)
    {
        try
        {
            var publisherLink = await FallbackToKavitaReaderPublisher(publisherName);
            if (string.IsNullOrEmpty(publisherLink))
            {
                throw new KavitaException($"Could not grab publisher image for {publisherName}");
            }

            var finalUrl = publisherLink;

            _logger.LogTrace("Fetching publisher image from {Url}", finalUrl);
            // Download the favicon.ico file using Flurl
            var publisherStream = await finalUrl
                .AllowHttpStatus("2xx,304")
                .GetStreamAsync();

            // Create the destination file path
            using var image = _imageFactory.Create(publisherStream);
            var filename = GetPublisherFormat(publisherName, encodeFormat);
            await image.SaveAsync(Path.Combine(_directoryService.FaviconDirectory, filename), encodeFormat, quality);
            _logger.LogDebug("Publisher image for {PublisherName} downloaded and saved successfully", publisherName);
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PublisherName}", publisherName);
            throw;
        }
    }

    private (Vector3?, Vector3?) GetPrimarySecondaryColors(string imagePath)
    {


        var rgbPixels = _imageFactory.GetRgbPixelsPercentage(imagePath, 10);

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

    private static bool IsColorCloseToWhiteOrBlack(Vector3 color)
    {
        var (_, _, lightness) = RgbToHsl(color);
        return lightness is > WhiteThreshold or < BlackThreshold;
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

    public static List<Vector3> SortByBrightness(List<Vector3> colors)
    {
        return colors.OrderBy(c => 0.299 * c.X + 0.587 * c.Y + 0.114 * c.Z).ToList();
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
        var threshold = 30;
        return (color.X > 255 - threshold && color.Y > 255 - threshold && color.Z > 255 - threshold) ||
               (color.X < threshold && color.Y < threshold && color.Z < threshold);
    }

    private static string RgbToHex(Vector3 color)
    {
        return $"#{(int)color.X:X2}{(int)color.Y:X2}{(int)color.Z:X2}";
    }

    private static Vector3 GetComplementaryColor(Vector3 color)
    {
        // Convert RGB to HSL
        var (h, s, l) = RgbToHsl(color);

        // Rotate hue by 180 degrees
        h = (h + 180) % 360;

        // Convert back to RGB
        return HslToRgb(h, s, l);
    }

    private static (double H, double S, double L) RgbToHsl(Vector3 rgb)
    {
        double r = rgb.X / 255;
        double g = rgb.Y / 255;
        double b = rgb.Z / 255;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var diff = max - min;

        double h = 0;
        double s = 0;
        var l = (max + min) / 2;

        if (Math.Abs(diff) > 0.00001)
        {
            s = l > 0.5 ? diff / (2 - max - min) : diff / (max + min);

            if (max == r)
                h = (g - b) / diff + (g < b ? 6 : 0);
            else if (max == g)
                h = (b - r) / diff + 2;
            else if (max == b)
                h = (r - g) / diff + 4;

            h *= 60;
        }

        return (h, s, l);
    }

    private static Vector3 HslToRgb(double h, double s, double l)
    {
        double r, g, b;

        if (Math.Abs(s) < 0.00001)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 120);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 120);
        }

        return new Vector3((float)(r * 255), (float)(g * 255), (float)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 360;
        if (t > 360) t -= 360;
        return t switch
        {
            < 60 => p + (q - p) * t / 60,
            < 180 => q,
            < 240 => p + (q - p) * (240 - t) / 60,
            _ => p
        };
    }

    /// <summary>
    /// Generates the Primary and Secondary colors from a file
    /// </summary>
    /// <remarks>This may use a second most common color or a complementary color. It's up to implemenation to choose what's best</remarks>
    /// <param name="sourceFile"></param>
    /// <returns></returns>
    public ColorScape CalculateColorScape(string sourceFile)
    {
        if (!File.Exists(sourceFile)) return new ColorScape() {Primary = null, Secondary = null};

        var colors = GetPrimarySecondaryColors(sourceFile);

        return new ColorScape()
        {
            Primary = colors.Item1 == null ? null : RgbToHex(colors.Item1.Value),
            Secondary = colors.Item2 == null ? null : RgbToHex(colors.Item2.Value)
        };
    }
    private bool CheckDirectSupport(string filename, List<string> supportedImageFormats)
    {
        if (supportedImageFormats == null) return false;

        string ext = Path.GetExtension(filename).ToLowerInvariant().Substring(1);
        return supportedImageFormats.Contains(ext);
    }


    /// <inheritdoc/>
    public string ReplaceImageFileFormat(string filename, List<string> supportedImageFormats = null, EncodeFormat format = EncodeFormat.JPEG, int quality = 99)
    {
        if (CheckDirectSupport(filename, supportedImageFormats)) return filename;

        Match m = Regex.Match(Path.GetExtension(filename), Parser.NonUniversalFileImageExtensions, RegexOptions.IgnoreCase, Parser.RegexTimeout);
        if (!m.Success) return filename;

        string destination = Path.ChangeExtension(filename, format.GetExtension().Substring(1));

        // Adding a lock per destination, sometimes web ui triggers same image loading at the ~ same time.
        // So, if other thread is already processing the image, we should wait for it to finish, then the File.Exists(destination) will early exit.
        // This exists, to prevent multiple threads from processing the same image at the same time.
        lock (_lock[destination])
        {
            if (File.Exists(destination))
            {
                // Destination already exist, the conversion was already made.
                return destination;
            }
            using var sourceImage = _imageFactory.Create(filename);
            sourceImage.Save(destination, format, quality);
            try
            {
                File.Delete(filename);
            }
            catch (Exception) { /* Swallow Exception */ }
        }
        return destination;
    }

    private static async Task<string> FallbackToKavitaReaderFavicon(string baseUrl)
    {
        var correctSizeLink = string.Empty;
        var allOverrides = await "https://www.kavitareader.com/assets/favicons/urls.txt".GetStringAsync();
        if (!string.IsNullOrEmpty(allOverrides))
        {
            var cleanedBaseUrl = baseUrl.Replace("https://", string.Empty);
            var externalFile = allOverrides
                .Split("\n")
                .FirstOrDefault(url =>
                    cleanedBaseUrl.Equals(url.Replace(".png", string.Empty)) ||
                    cleanedBaseUrl.Replace("www.", string.Empty).Equals(url.Replace(".png", string.Empty)
                    ));

            if (string.IsNullOrEmpty(externalFile))
            {
                throw new KavitaException($"Could not grab favicon from {baseUrl}");
            }

            correctSizeLink = "https://www.kavitareader.com/assets/favicons/" + externalFile;
        }

        return correctSizeLink;
    }

    private static async Task<string> FallbackToKavitaReaderPublisher(string publisherName)
    {
        var externalLink = string.Empty;
        var allOverrides = await "https://www.kavitareader.com/assets/publishers/publishers.txt".GetStringAsync();
        if (!string.IsNullOrEmpty(allOverrides))
        {
            var externalFile = allOverrides
                .Split("\n")
                .Select(publisherLine =>
                {
                    var tokens = publisherLine.Split("|");
                    if (tokens.Length != 2) return null;
                    var aliases = tokens[0];
                    // Multiple publisher aliases are separated by #
                    if (aliases.Split("#").Any(name => name.ToLowerInvariant().Trim().Equals(publisherName.ToLowerInvariant().Trim())))
                    {
                        return tokens[1];
                    }
                    return null;
                })
                .FirstOrDefault(url => !string.IsNullOrEmpty(url));

            if (string.IsNullOrEmpty(externalFile))
            {
                throw new KavitaException($"Could not grab publisher image for {publisherName}");
            }

            externalLink = "https://www.kavitareader.com/assets/publishers/" + externalFile;
        }

        return externalLink;
    }

    /// <inheritdoc />
    public string CreateThumbnailFromBase64(string encodedImage, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = ThumbnailWidth, int quality = 100)
    {
        try
        {

            using var thumbnail = _imageFactory.CreateFromBase64(encodedImage);
            int thumbnailHeight = (int)(thumbnail.Height * ((double)thumbnailWidth / thumbnail.Width));
            thumbnail.Thumbnail(thumbnailWidth, thumbnailHeight);
            fileName += encodeFormat.GetExtension();
            thumbnail.Save(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, fileName), encodeFormat, quality);
            return fileName;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating thumbnail from url");
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

    public static string GetWebLinkFormat(string url, EncodeFormat encodeFormat)
    {
        return $"{new Uri(url).Host.Replace("www.", string.Empty)}{encodeFormat.GetExtension()}";
    }

    public static string GetPublisherFormat(string publisher, EncodeFormat encodeFormat)
    {
        return $"{publisher}{encodeFormat.GetExtension()}";
    }

    /// <inheritdoc/>
    public void CreateMergedImage(IList<string> coverImages, CoverImageSize size, string dest, EncodeFormat format = EncodeFormat.PNG, int quality = 100)
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


        var image = _imageFactory.Create(width, height);

        var thumbnailWidth = image.Width / cols;
        var thumbnailHeight = image.Height / rows;

        for (var i = 0; i < coverImages.Count; i++)
        {
            if (!File.Exists(coverImages[i])) continue;
            var tile = _imageFactory.Create(coverImages[i]);
            tile.Thumbnail(thumbnailWidth, thumbnailHeight);

            var row = i / cols;
            var col = i % cols;

            var x = col * thumbnailWidth;
            var y = row * thumbnailHeight;

            if (coverImages.Count == 3 && i == 2)
            {
                x = (image.Width - thumbnailWidth) / 2;
                y = thumbnailHeight;
            }
            image.Composite(tile,x,y);
        }

        image.Save(dest, format, quality);
    }

    /// <inheritdoc/>
    public void UpdateColorScape(IHasCoverImage entity)
    {
        var colors = CalculateColorScape(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, entity.CoverImage));
        entity.PrimaryColor = colors.Primary;
        entity.SecondaryColor = colors.Secondary;
    }

    public static Color HexToRgb(string? hex)
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

        return Color.FromArgb(r, g, b);
    }


}
