using System;
using System.IO;
using System.Linq;
using API.Comparators;
using API.Entities;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services;

public interface IImageService
{
    void ExtractImages(string fileFilePath, string targetDirectory, int fileCount);
    string GetCoverImage(string path, string fileName);
    string GetCoverFile(MangaFile file);

    /// <summary>
    /// Creates a Thumbnail version of an image
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    string CreateThumbnail(string path, string fileName);
    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    string CreateThumbnailFromBase64(string encodedImage, string fileName);
}

public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly IDirectoryService _directoryService;
    public const string ChapterCoverImageRegex = @"v\d+_c\d+";
    public const string SeriesCoverImageRegex = @"seres\d+";
    public const string CollectionTagCoverImageRegex = @"tag\d+";


    /// <summary>
    /// Width of the Thumbnail generation
    /// </summary>
    private const int ThumbnailWidth = 320;

    public ImageService(ILogger<ImageService> logger, IDirectoryService directoryService)
    {
        _logger = logger;
        _directoryService = directoryService;
    }

    public void ExtractImages(string fileFilePath, string targetDirectory, int fileCount = 1)
    {
        DirectoryService.ExistOrCreate(targetDirectory);
        if (fileCount == 1)
        {
            _directoryService.CopyFileToDirectory(fileFilePath, targetDirectory);
        }
        else
        {
            DirectoryService.CopyDirectoryToDirectory(Path.GetDirectoryName(fileFilePath), targetDirectory,
                Parser.Parser.ImageFileExtensions);
        }
    }

    /// <summary>
    /// Finds the first image in the directory of the first file. Does not check for "cover/folder".ext files to override.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public string GetCoverFile(MangaFile file)
    {
        var directory = Path.GetDirectoryName(file.FilePath);
        if (string.IsNullOrEmpty(directory))
        {
            _logger.LogError("Could not find Directory for {File}", file.FilePath);
            return null;
        }

        var firstImage = DirectoryService.GetFilesWithExtension(directory, Parser.Parser.ImageFileExtensions)
            .OrderBy(f => f, new NaturalSortComparer()).FirstOrDefault();

        return firstImage;
    }

    public string GetCoverImage(string path, string fileName)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        try
        {
            return CreateThumbnail(path,  fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public string CreateThumbnail(string path, string fileName)
    {
        try
        {
            using var thumbnail = Image.Thumbnail(path, ThumbnailWidth);
            var filename = fileName + ".png";
            thumbnail.WriteToFile(Path.Join(DirectoryService.CoverImageDirectory, filename));
            return filename;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating thumbnail from url");
        }

        return string.Empty;
    }

    /// <summary>
    /// Creates a thumbnail out of a memory stream and saves to <see cref="DirectoryService.CoverImageDirectory"/> with the passed
    /// fileName and .png extension.
    /// </summary>
    /// <param name="stream">Stream to write to disk. Ensure this is rewinded.</param>
    /// <param name="fileName">filename to save as without extension</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    public static string WriteCoverThumbnail(Stream stream, string fileName)
    {
        using var thumbnail = Image.ThumbnailStream(stream, ThumbnailWidth);
        var filename = fileName + ".png";
        thumbnail.WriteToFile(Path.Join(DirectoryService.CoverImageDirectory, fileName + ".png"));
        return filename;
    }


    /// <inheritdoc />
    public string CreateThumbnailFromBase64(string encodedImage, string fileName)
    {
        try
        {
            using var thumbnail = Image.ThumbnailBuffer(Convert.FromBase64String(encodedImage), ThumbnailWidth);
            var filename = fileName + ".png";
            thumbnail.WriteToFile(Path.Join(DirectoryService.CoverImageDirectory, fileName + ".png"));
            return filename;
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
}
