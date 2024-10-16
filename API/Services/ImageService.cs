﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Extensions;
using EasyCaching.Core;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using NetVips;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Image = NetVips.Image;

namespace API.Services;
#nullable enable

public interface IImageService
{
    void ExtractImages(string fileFilePath, string targetDirectory, int fileCount = 1);
    string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size);

    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <param name="fileName"></param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="thumbnailWidth">Width of thumbnail</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    string CreateThumbnailFromBase64(string encodedImage, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = 320);
    /// <summary>
    /// Writes out a thumbnail by stream input
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="fileName"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="encodeFormat"></param>
    /// <returns></returns>
    string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    /// <summary>
    /// Writes out a thumbnail by file path input
    /// </summary>
    /// <param name="sourceFile"></param>
    /// <param name="fileName"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="encodeFormat"></param>
    /// <returns></returns>
    string WriteCoverThumbnail(string sourceFile, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    /// <summary>
    /// Converts the passed image to encoding and outputs it in the same directory
    /// </summary>
    /// <param name="filePath">Full path to the image to convert</param>
    /// <param name="outputPath">Where to output the file</param>
    /// <param name="encodeFormat">Encoding Format</param>
    /// <returns>File of written encoded image</returns>
    Task<string> ConvertToEncodingFormat(string filePath, string outputPath, EncodeFormat encodeFormat);
    Task<bool> IsImage(string filePath);
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat);
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat);
    void UpdateColorScape(IHasCoverImage entity);
}

public class ImageService : IImageService
{
    public const string Name = "BookmarkService";
    private readonly ILogger<ImageService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory;
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

    public ImageService(ILogger<ImageService> logger, IDirectoryService directoryService, IEasyCachingProviderFactory cacheFactory)
    {
        _logger = logger;
        _directoryService = directoryService;
        _cacheFactory = cacheFactory;
    }

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
            thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
            return filename;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
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
        _directoryService.ExistOrCreate(outputDirectory);

        try
        {
            _directoryService.FileSystem.File.Delete(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}

        try
        {
            thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, filename));

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
            thumbnail2.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, filename));

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
        _directoryService.ExistOrCreate(outputDirectory);
        try
        {
            _directoryService.FileSystem.File.Delete(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}
        thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        return filename;
    }

    public Task<string> ConvertToEncodingFormat(string filePath, string outputPath, EncodeFormat encodeFormat)
    {
        var file = _directoryService.FileSystem.FileInfo.New(filePath);
        var fileName = file.Name.Replace(file.Extension, string.Empty);
        var outputFile = Path.Join(outputPath, fileName + encodeFormat.GetExtension());

        using var sourceImage = Image.NewFromFile(filePath, false, Enums.Access.SequentialUnbuffered);
        sourceImage.WriteToFile(outputFile);
        return Task.FromResult(outputFile);
    }

    /// <summary>
    /// Performs I/O to determine if the file is a valid Image
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public async Task<bool> IsImage(string filePath)
    {
        try
        {
            var info = await SixLabors.ImageSharp.Image.IdentifyAsync(filePath);
            if (info == null) return false;

            return true;
        }
        catch (Exception)
        {
            /* Swallow Exception */
        }

        return false;
    }

    public async Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat)
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
            using var image = Image.PngloadStream(faviconStream);
            var filename = ImageService.GetWebLinkFormat(baseUrl, encodeFormat);
            switch (encodeFormat)
            {
                case EncodeFormat.PNG:
                    image.Pngsave(Path.Combine(_directoryService.FaviconDirectory, filename));
                    break;
                case EncodeFormat.WEBP:
                    image.Webpsave(Path.Combine(_directoryService.FaviconDirectory, filename));
                    break;
                case EncodeFormat.AVIF:
                    image.Heifsave(Path.Combine(_directoryService.FaviconDirectory, filename));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null);
            }


            _logger.LogDebug("Favicon for {Domain} downloaded and saved successfully", domain);
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading favicon for {Domain}", domain);
            throw;
        }
    }

    public async Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat)
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
            using var image = Image.PngloadStream(publisherStream);
            var filename = GetPublisherFormat(publisherName, encodeFormat);
            switch (encodeFormat)
            {
                case EncodeFormat.PNG:
                    image.Pngsave(Path.Combine(_directoryService.PublisherDirectory, filename));
                    break;
                case EncodeFormat.WEBP:
                    image.Webpsave(Path.Combine(_directoryService.PublisherDirectory, filename));
                    break;
                case EncodeFormat.AVIF:
                    image.Heifsave(Path.Combine(_directoryService.PublisherDirectory, filename));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null);
            }


            _logger.LogDebug("Publisher image for {PublisherName} downloaded and saved successfully", publisherName);
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PublisherName}", publisherName);
            throw;
        }
    }

    private static (Vector3?, Vector3?) GetPrimarySecondaryColors(string imagePath)
    {
        using var image = Image.NewFromFile(imagePath);
        // Resize the image to speed up processing
        var resizedImage = image.Resize(0.1);

        var processedImage = PreProcessImage(resizedImage);


        // Convert image to RGB array
        var pixels = processedImage.WriteToMemory().ToArray();

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

    private static (Vector3?, Vector3?) GetPrimaryColorSharp(string imagePath)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(imagePath);

        image.Mutate(
            x => x
                // Scale the image down preserving the aspect ratio. This will speed up quantization.
                // We use nearest neighbor as it will be the fastest approach.
                .Resize(new ResizeOptions() { Sampler = KnownResamplers.NearestNeighbor, Size = new SixLabors.ImageSharp.Size(100, 0) })

                // Reduce the color palette to 1 color without dithering.
                .Quantize(new OctreeQuantizer(new QuantizerOptions { MaxColors = 4 })));

        Rgb24 dominantColor = image[0, 0];

        // This will give you a dominant color in HEX format i.e #5E35B1FF
        return (new Vector3(dominantColor.R, dominantColor.G, dominantColor.B), new Vector3(dominantColor.R, dominantColor.G, dominantColor.B));
    }

    private static Image PreProcessImage(Image image)
    {
        return image;
        // Create a mask for white and black pixels
        var whiteMask = image.Colourspace(Enums.Interpretation.Lab)[0] > (WhiteThreshold * 100);
        var blackMask = image.Colourspace(Enums.Interpretation.Lab)[0] < (BlackThreshold * 100);

        // Create a replacement color (e.g., medium gray)
        var replacementColor = new[] { 240.0, 240.0, 240.0 };

        // Apply the masks to replace white and black pixels
        var processedImage = image.Copy();
        processedImage = processedImage.Ifthenelse(whiteMask, replacementColor);
        //processedImage = processedImage.Ifthenelse(blackMask, replacementColor);

        return processedImage;
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

    private static bool IsColorCloseToWhiteOrBlack(Vector3 color)
    {
        var (_, _, lightness) = RgbToHsl(color);
        return lightness is > WhiteThreshold or < BlackThreshold;
    }

    public static List<Vector3> KMeansClustering(List<Vector3> points, int k, int maxIterations = 100)
	{
		// Initialize centroids using k-means++ for better starting positions
		var centroids = InitializeCentroidsKMeansPlusPlus(points, k);

		var assignments = new int[points.Count];
		var clusters = new List<int>[k];
		for (int i = 0; i < k; i++)
		{
			clusters[i] = new List<int>();
		}

		for (var iteration = 0; iteration < maxIterations; iteration++)
		{
			bool centroidsChanged = false;

			foreach (var cluster in clusters)
			{
				cluster.Clear();
			}

			// Assign points to the nearest centroid
			Parallel.For(0, points.Count, i =>
			{
				var point = points[i];
				int nearestCentroidIndex = 0;
				float minDistanceSquared = float.MaxValue;

				for (int c = 0; c < k; c++)
				{
					var centroid = centroids[c];
					float dx = point.X - centroid.X;
					float dy = point.Y - centroid.Y;
					float dz = point.Z - centroid.Z;
					float distanceSquared = dx * dx + dy * dy + dz * dz;

					if (distanceSquared < minDistanceSquared)
					{
						minDistanceSquared = distanceSquared;
						nearestCentroidIndex = c;
					}
				}

				assignments[i] = nearestCentroidIndex;
			});

			// Build clusters
			for (int i = 0; i < points.Count; i++)
			{
				clusters[assignments[i]].Add(i);
			}

			// Update centroids
			for (int c = 0; c < k; c++)
			{
				var cluster = clusters[c];
				if (cluster.Count == 0)
					continue;

				float sumX = 0, sumY = 0, sumZ = 0;
				foreach (var index in cluster)
				{
					var point = points[index];
					sumX += point.X;
					sumY += point.Y;
					sumZ += point.Z;
				}

				var count = cluster.Count;
				var newCentroid = new Vector3(sumX / count, sumY / count, sumZ / count);

				// Check if centroids have changed significantly
				if (!IsCentroidConverged(centroids[c], newCentroid))
				{
					centroidsChanged = true;
					centroids[c] = newCentroid;
				}
			}

			if (!centroidsChanged)
				break;
		}

		return centroids;
	}
	// K-means++ initialization for better starting centroids
	private static List<Vector3> InitializeCentroidsKMeansPlusPlus(List<Vector3> points, int k)
	{
		var random = new Random();
		var centroids = new List<Vector3> { points[random.Next(points.Count)] };
		var distances = new float[points.Count];

		for (int i = 1; i < k; i++)
		{
			float totalDistance = 0;
			for (int p = 0; p < points.Count; p++)
			{
				var point = points[p];
				var minDistance = float.MaxValue;

				foreach (var centroid in centroids)
				{
					var dx = point.X - centroid.X;
					var dy = point.Y - centroid.Y;
					var dz = point.Z - centroid.Z;
					var distanceSquared = dx * dx + dy * dy + dz * dz;

					if (distanceSquared < minDistance)
					{
						minDistance = distanceSquared;
					}
				}
				distances[p] = minDistance;
				totalDistance += minDistance;
			}

			var targetDistance = random.NextDouble() * totalDistance;
			totalDistance = 0;

			for (int p = 0; p < points.Count; p++)
			{
				totalDistance += distances[p];
				if (totalDistance >= targetDistance)
				{
					centroids.Add(points[p]);
					break;
				}
			}
		}

		return centroids;
	}

	// Helper method to check centroid convergence with a tolerance
	private static bool IsCentroidConverged(Vector3 oldCentroid, Vector3 newCentroid, float tolerance = 0.0001f)
	{
		return Vector3.DistanceSquared(oldCentroid, newCentroid) <= tolerance * tolerance;
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
    public string CreateThumbnailFromBase64(string encodedImage, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = ThumbnailWidth)
    {
        try
        {
            using var thumbnail = Image.ThumbnailBuffer(Convert.FromBase64String(encodedImage), thumbnailWidth);
            fileName += encodeFormat.GetExtension();
            thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, fileName));
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
            _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, entity.CoverImage));
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
