﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetVips;
using Image = NetVips.Image;

namespace API.Services;

public interface IImageService
{
    void ExtractImages(string fileFilePath, string targetDirectory, int fileCount = 1);
    string GetCoverImage(string path, string fileName, string outputDirectory, bool saveAsWebP = false);

    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <param name="fileName"></param>
    /// <param name="saveAsWebP">Convert and save as webp</param>
    /// <param name="thumbnailWidth">Width of thumbnail</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    string CreateThumbnailFromBase64(string encodedImage, string fileName, bool saveAsWebP = false, int thumbnailWidth = 320);

    string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, bool saveAsWebP = false);
    /// <summary>
    /// Converts the passed image to webP and outputs it in the same directory
    /// </summary>
    /// <param name="filePath">Full path to the image to convert</param>
    /// <param name="outputPath">Where to output the file</param>
    /// <returns>File of written webp image</returns>
    Task<string> ConvertToWebP(string filePath, string outputPath);

    Task<bool> IsImage(string filePath);
}

public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly IDirectoryService _directoryService;
    public const string ChapterCoverImageRegex = @"v\d+_c\d+";
    public const string SeriesCoverImageRegex = @"series\d+";
    public const string CollectionTagCoverImageRegex = @"tag\d+";
    public const string ReadingListCoverImageRegex = @"readinglist\d+";

    /// <summary>
    /// Width of the Thumbnail generation
    /// </summary>
    private const int ThumbnailWidth = 320;
    /// <summary>
    /// Width of a cover for Library
    /// </summary>
    public const int LibraryThumbnailWidth = 32;

    public ImageService(ILogger<ImageService> logger, IDirectoryService directoryService)
    {
        _logger = logger;
        _directoryService = directoryService;
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

    public string GetCoverImage(string path, string fileName, string outputDirectory, bool saveAsWebP = false)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        try
        {
            using var thumbnail = Image.Thumbnail(path, ThumbnailWidth);
            var filename = fileName + (saveAsWebP ? ".webp" : ".png");
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
    /// fileName and .png extension.
    /// </summary>
    /// <param name="stream">Stream to write to disk. Ensure this is rewinded.</param>
    /// <param name="fileName">filename to save as without extension</param>
    /// <param name="outputDirectory">Where to output the file, defaults to covers directory</param>
    /// <param name="saveAsWebP">Export the file as webP otherwise will default to png</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    public string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, bool saveAsWebP = false)
    {
        using var thumbnail = Image.ThumbnailStream(stream, ThumbnailWidth);
        var filename = fileName + (saveAsWebP ? ".webp" : ".png");
        _directoryService.ExistOrCreate(outputDirectory);
        try
        {
            _directoryService.FileSystem.File.Delete(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        } catch (Exception) {/* Swallow exception */}
        thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, filename));
        return filename;
    }

    public Task<string> ConvertToWebP(string filePath, string outputPath)
    {
        var file = _directoryService.FileSystem.FileInfo.New(filePath);
        var fileName = file.Name.Replace(file.Extension, string.Empty);
        var outputFile = Path.Join(outputPath, fileName + ".webp");

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


    /// <inheritdoc />
    public string CreateThumbnailFromBase64(string encodedImage, string fileName, bool saveAsWebP = false, int thumbnailWidth = ThumbnailWidth)
    {
        try
        {
            using var thumbnail = Image.ThumbnailBuffer(Convert.FromBase64String(encodedImage), thumbnailWidth);
            fileName += (saveAsWebP ? ".webp" : ".png");
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
        return $"readinglist{readingListId}";
    }


    public static string CreateMergedImage(List<string> coverImages, string dest)
    {
        // TODO: Needs testing
        // Currently this doesn't work due to non-standard cover image sizes and dimensions
        var image = Image.Black(320*4, 160*4);

        for (var i = 0; i < coverImages.Count; i++)
        {
            var tile = Image.NewFromFile(coverImages[i], access: Enums.Access.Sequential);

            var x = (i % 2) * (image.Width / 2);
            var y = (i / 2) * (image.Height / 2);

            image = image.Insert(tile, x, y);
        }

        image.WriteToFile(dest);
        return dest;
    }
}
