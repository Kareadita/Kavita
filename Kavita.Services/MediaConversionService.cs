using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Services.Comparators;
using Kavita.Services.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class MediaConversionService(
    IUnitOfWork unitOfWork,
    IImageService imageService,
    IEventHub eventHub,
    IDirectoryService directoryService,
    ILogger<MediaConversionService> logger)
    : IMediaConversionService
{
    public const string Name = "MediaConversionService";
    public static readonly string[] ConversionMethods = ["ConvertAllBookmarkToEncoding", "ConvertAllCoversToEncoding", "ConvertAllManagedMediaToEncodingFormat"];

    /// <summary>
    /// Converts all Kavita managed media (bookmarks, covers, favicons, etc) to the saved target encoding.
    /// Do not invoke anyway except via Hangfire.
    /// </summary>
    /// <param name="ct"></param>
    /// <remarks>This is a long-running job</remarks>
    /// <returns></returns>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllManagedMediaToEncodingFormat(CancellationToken ct = default)
    {
        await ConvertAllBookmarkToEncoding(ct);
        await ConvertAllCoversToEncoding(ct);
        await CoverAllFaviconsToEncoding(ct);

    }

    /// <summary>
    /// This is a long-running job that will convert all bookmarks into a format that is not PNG. Do not invoke anyway except via Hangfire.
    /// </summary>
    /// <param name="ct"></param>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllBookmarkToEncoding(CancellationToken ct = default)
    {
        var bookmarkDirectory =
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory, ct)).Value;
        var encodeFormat =
            (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            logger.LogError("Cannot convert media to PNG");
            return;
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(0F, ProgressEventType.Started), ct: ct);

        var bookmarks = (await unitOfWork.UserRepository.GetAllBookmarksAsync(ct))
            .Where(b => !b.FileName.EndsWith(encodeFormat.GetExtension())).ToList();

        var count = 1F;
        foreach (var bookmark in bookmarks)
        {
            bookmark.FileName = await SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
                BookmarkService.BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId), encodeFormat);

            unitOfWork.UserRepository.Update(bookmark);

            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertBookmarksProgressEvent(count / bookmarks.Count, ProgressEventType.Updated), ct: ct);

            count++;
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(1F, ProgressEventType.Ended), ct: ct);

        logger.LogInformation("[MediaConversionService] Converted bookmarks to {Format}", encodeFormat);
    }

    /// <summary>
    /// This is a long-running job that will convert all covers into WebP. Do not invoke anyway except via Hangfire.
    /// </summary>
    /// <param name="ct"></param>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllCoversToEncoding(CancellationToken ct = default)
    {
        var coverDirectory = directoryService.CoverImageDirectory;
        var encodeFormat =
            (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            logger.LogError("Cannot convert media to PNG");
            return;
        }

        logger.LogInformation("[MediaConversionService] Starting conversion of all covers to {Format}", encodeFormat);
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(0F, ProgressEventType.Started), ct: ct);

        var chapterCovers = await unitOfWork.ChapterRepository.GetAllChaptersWithCoversInDifferentEncoding(encodeFormat, ct);
        var customSeriesCovers = await unitOfWork.SeriesRepository.GetAllWithCoversInDifferentEncodingAsync(encodeFormat);
        var seriesCovers = await unitOfWork.SeriesRepository.GetAllWithCoversInDifferentEncodingAsync(encodeFormat, false);
        var nonCustomOrConvertedVolumeCovers = await unitOfWork.VolumeRepository.GetAllWithCoversInDifferentEncoding(encodeFormat, ct);

        var readingListCovers = await unitOfWork.ReadingListRepository.GetAllWithCoversInDifferentEncoding(encodeFormat, ct);
        var libraryCovers = await unitOfWork.LibraryRepository.GetAllWithCoversInDifferentEncoding(encodeFormat, ct);
        var collectionCovers = await unitOfWork.CollectionTagRepository.GetAllWithCoversInDifferentEncoding(encodeFormat, ct);

        var totalCount = chapterCovers.Count + seriesCovers.Count + readingListCovers.Count +
                         libraryCovers.Count + collectionCovers.Count + nonCustomOrConvertedVolumeCovers.Count + customSeriesCovers.Count;

        var count = 1F;
        logger.LogInformation("[MediaConversionService] Starting conversion of chapters");
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(0, ProgressEventType.Started), ct: ct);
        logger.LogInformation("[MediaConversionService] Starting conversion of libraries");
        foreach (var library in libraryCovers)
        {
            if (string.IsNullOrEmpty(library.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, library.CoverImage, coverDirectory, encodeFormat);
            library.CoverImage = Path.GetFileName(newFile);

            unitOfWork.LibraryRepository.Update(library);

            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated), ct: ct);

            count++;
        }

        logger.LogInformation("[MediaConversionService] Starting conversion of reading lists");
        foreach (var readingList in readingListCovers)
        {
            if (string.IsNullOrEmpty(readingList.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, readingList.CoverImage, coverDirectory, encodeFormat);
            readingList.CoverImage = Path.GetFileName(newFile);

            unitOfWork.ReadingListRepository.Update(readingList);

            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated), ct: ct);

            count++;
        }

        logger.LogInformation("[MediaConversionService] Starting conversion of collections");
        foreach (var collection in collectionCovers)
        {
            if (string.IsNullOrEmpty(collection.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, collection.CoverImage, coverDirectory, encodeFormat);
            collection.CoverImage = Path.GetFileName(newFile);

            unitOfWork.CollectionTagRepository.Update(collection);

            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated), ct: ct);

            count++;
        }

        logger.LogInformation("[MediaConversionService] Starting conversion of chapters");
        foreach (var chapter in chapterCovers)
        {
            if (string.IsNullOrEmpty(chapter.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, chapter.CoverImage, coverDirectory, encodeFormat);
            chapter.CoverImage = Path.GetFileName(newFile);

            unitOfWork.ChapterRepository.Update(chapter);

            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated), ct: ct);

            count++;
        }

        // Now null out all series and volumes that aren't webp or custom
        logger.LogInformation("[MediaConversionService] Starting conversion of volumes");
        foreach (var volume in nonCustomOrConvertedVolumeCovers)
        {
            if (string.IsNullOrEmpty(volume.CoverImage)) continue;
            volume.CoverImage = volume.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
            unitOfWork.VolumeRepository.Update(volume);
            await unitOfWork.CommitAsync(ct);
        }

        logger.LogInformation("[MediaConversionService] Starting conversion of series");
        foreach (var series in customSeriesCovers)
        {
            if (string.IsNullOrEmpty(series.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, series.CoverImage, coverDirectory, encodeFormat);
            series.CoverImage = string.IsNullOrEmpty(newFile) ?
                series.CoverImage.Replace(Path.GetExtension(series.CoverImage), encodeFormat.GetExtension()) : Path.GetFileName(newFile);

            unitOfWork.SeriesRepository.Update(series);
            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated), ct: ct);
            count++;
        }

        foreach (var series in seriesCovers)
        {
            if (string.IsNullOrEmpty(series.CoverImage)) continue;
            series.CoverImage = series.GetCoverImage();
            if (series.CoverImage == null)
            {
                logger.LogDebug("[SeriesCoverImageBug] Setting Series Cover Image to null: {SeriesId}", series.Id);
            }
            unitOfWork.SeriesRepository.Update(series);
            await unitOfWork.CommitAsync(ct);
        }

        // Get all volumes and remap their covers

        // Get all series and remap their covers

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(1F, ProgressEventType.Ended), ct: ct);

        logger.LogInformation("[MediaConversionService] Converted covers to {Format}", encodeFormat);
    }

    private async Task CoverAllFaviconsToEncoding(CancellationToken ct = default)
    {
        var encodeFormat =
            (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            logger.LogError("Cannot convert media to PNG");
            return;
        }

        logger.LogInformation("[MediaConversionService] Starting conversion of favicons to {Format}", encodeFormat);
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(0F, ProgressEventType.Started), ct: ct);
        var pngFavicons = directoryService.GetFiles(directoryService.FaviconDirectory)
            .Where(b => !b.EndsWith(encodeFormat.GetExtension())).
            ToList();

        var count = 1F;
        foreach (var file in pngFavicons)
        {
            await SaveAsEncodingFormat(directoryService.FaviconDirectory, directoryService.FileSystem.FileInfo.New(file).Name, directoryService.FaviconDirectory,
                encodeFormat);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertBookmarksProgressEvent(count / pngFavicons.Count, ProgressEventType.Updated), ct: ct);
            count++;
        }


        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(1F, ProgressEventType.Ended), ct: ct);

        logger.LogInformation("[MediaConversionService] Converted favicons to {Format}", encodeFormat);
    }


    /// <summary>
    /// Converts an image file, deletes original and returns the new path back
    /// </summary>
    /// <param name="imageDirectory">Full Path to where files are stored</param>
    /// <param name="filename">The file to convert</param>
    /// <param name="targetFolder">Full path to where files should be stored or any stem</param>
    /// <param name="encodeFormat">Encoding Format</param>
    /// <returns></returns>
    public async Task<string> SaveAsEncodingFormat(string imageDirectory, string filename, string targetFolder, EncodeFormat encodeFormat)
    {
        // This must be Public as it's used in via Hangfire as a background task
        var fullSourcePath = directoryService.FileSystem.Path.Join(imageDirectory, filename);
        var fullTargetDirectory = fullSourcePath.Replace(new FileInfo(filename).Name, string.Empty);

        var newFilename = string.Empty;
        logger.LogDebug("Converting {Source} image into {Encoding} at {Target}", fullSourcePath, encodeFormat, fullTargetDirectory);

        if (!File.Exists(fullSourcePath))
        {
            logger.LogError("Requested to convert {File} but it doesn't exist", fullSourcePath);
            return newFilename;
        }

        try
        {
            // Convert target file to format then delete original target file
            try
            {
                var targetFile = await imageService.ConvertToEncodingFormat(fullSourcePath, fullTargetDirectory, encodeFormat);
                var targetName = new FileInfo(targetFile).Name;
                newFilename = Path.Join(targetFolder, targetName);
                directoryService.DeleteFiles([fullSourcePath]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not convert image {FilePath} to {Format}", filename, encodeFormat);
                newFilename = filename;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not convert image to {Format}", encodeFormat);
        }

        return newFilename;
    }

}
