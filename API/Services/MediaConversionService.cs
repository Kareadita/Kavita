using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Entities.Enums;
using API.Extensions;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IMediaConversionService
{
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllBookmarkToEncoding();
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllCoversToEncoding();
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllManagedMediaToEncodingFormat();

    Task<string> SaveAsEncodingFormat(string imageDirectory, string filename, string targetFolder,
        EncodeFormat encodeFormat);
}

public class MediaConversionService : IMediaConversionService
{
    public const string Name = "MediaConversionService";
    public static readonly string[] ConversionMethods = {"ConvertAllBookmarkToEncoding", "ConvertAllCoversToEncoding", "ConvertAllManagedMediaToEncodingFormat"};
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageService _imageService;
    private readonly IEventHub _eventHub;
    private readonly IDirectoryService _directoryService;
    private readonly ILogger<MediaConversionService> _logger;

    public MediaConversionService(IUnitOfWork unitOfWork, IImageService imageService, IEventHub eventHub,
        IDirectoryService directoryService, ILogger<MediaConversionService> logger)
    {
        _unitOfWork = unitOfWork;
        _imageService = imageService;
        _eventHub = eventHub;
        _directoryService = directoryService;
        _logger = logger;
    }

     /// <summary>
    /// Converts all Kavita managed media (bookmarks, covers, favicons, etc) to the saved target encoding.
    /// Do not invoke anyway except via Hangfire.
    /// </summary>
    /// <remarks>This is a long-running job</remarks>
    /// <returns></returns>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllManagedMediaToEncodingFormat()
    {
        await ConvertAllBookmarkToEncoding();
        await ConvertAllCoversToEncoding();
        await CoverAllFaviconsToEncoding();

    }

    /// <summary>
    /// This is a long-running job that will convert all bookmarks into a format that is not PNG. Do not invoke anyway except via Hangfire.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllBookmarkToEncoding()
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        var encodeFormat =
            (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            _logger.LogError("Cannot convert media to PNG");
            return;
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(0F, ProgressEventType.Started));
        var bookmarks = (await _unitOfWork.UserRepository.GetAllBookmarksAsync())
            .Where(b => !b.FileName.EndsWith(encodeFormat.GetExtension())).ToList();

        var count = 1F;
        foreach (var bookmark in bookmarks)
        {
            bookmark.FileName = await SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
                BookmarkService.BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId), encodeFormat);
            _unitOfWork.UserRepository.Update(bookmark);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertBookmarksProgressEvent(count / bookmarks.Count, ProgressEventType.Updated));
            count++;
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(1F, ProgressEventType.Ended));

        _logger.LogInformation("[MediaConversionService] Converted bookmarks to {Format}", encodeFormat);
    }

    /// <summary>
    /// This is a long-running job that will convert all covers into WebP. Do not invoke anyway except via Hangfire.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllCoversToEncoding()
    {
        var coverDirectory = _directoryService.CoverImageDirectory;
        var encodeFormat =
            (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            _logger.LogError("Cannot convert media to PNG");
            return;
        }

        _logger.LogInformation("[MediaConversionService] Starting conversion of all covers to {Format}", encodeFormat);
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(0F, ProgressEventType.Started));

        var chapterCovers = await _unitOfWork.ChapterRepository.GetAllChaptersWithCoversInDifferentEncoding(encodeFormat);
        var customSeriesCovers = await _unitOfWork.SeriesRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);
        var seriesCovers = await _unitOfWork.SeriesRepository.GetAllWithCoversInDifferentEncoding(encodeFormat, false);
        var nonCustomOrConvertedVolumeCovers = await _unitOfWork.VolumeRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);

        var readingListCovers = await _unitOfWork.ReadingListRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);
        var libraryCovers = await _unitOfWork.LibraryRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);
        var collectionCovers = await _unitOfWork.CollectionTagRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);

        var totalCount = chapterCovers.Count + seriesCovers.Count + readingListCovers.Count +
                         libraryCovers.Count + collectionCovers.Count + nonCustomOrConvertedVolumeCovers.Count + customSeriesCovers.Count;

        var count = 1F;
        _logger.LogInformation("[MediaConversionService] Starting conversion of chapters");
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(0, ProgressEventType.Started));
        _logger.LogInformation("[MediaConversionService] Starting conversion of libraries");
        foreach (var library in libraryCovers)
        {
            if (string.IsNullOrEmpty(library.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, library.CoverImage, coverDirectory, encodeFormat);
            library.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.LibraryRepository.Update(library);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated));
            count++;
        }

        _logger.LogInformation("[MediaConversionService] Starting conversion of reading lists");
        foreach (var readingList in readingListCovers)
        {
            if (string.IsNullOrEmpty(readingList.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, readingList.CoverImage, coverDirectory, encodeFormat);
            readingList.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.ReadingListRepository.Update(readingList);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated));
            count++;
        }

        _logger.LogInformation("[MediaConversionService] Starting conversion of collections");
        foreach (var collection in collectionCovers)
        {
            if (string.IsNullOrEmpty(collection.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, collection.CoverImage, coverDirectory, encodeFormat);
            collection.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.CollectionTagRepository.Update(collection);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated));
            count++;
        }

        _logger.LogInformation("[MediaConversionService] Starting conversion of chapters");
        foreach (var chapter in chapterCovers)
        {
            if (string.IsNullOrEmpty(chapter.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, chapter.CoverImage, coverDirectory, encodeFormat);
            chapter.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.ChapterRepository.Update(chapter);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated));
            count++;
        }

        // Now null out all series and volumes that aren't webp or custom
        _logger.LogInformation("[MediaConversionService] Starting conversion of volumes");
        foreach (var volume in nonCustomOrConvertedVolumeCovers)
        {
            if (string.IsNullOrEmpty(volume.CoverImage)) continue;
            volume.CoverImage = volume.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
            _unitOfWork.VolumeRepository.Update(volume);
            await _unitOfWork.CommitAsync();
        }

        _logger.LogInformation("[MediaConversionService] Starting conversion of series");
        foreach (var series in customSeriesCovers)
        {
            if (string.IsNullOrEmpty(series.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, series.CoverImage, coverDirectory, encodeFormat);
            series.CoverImage = string.IsNullOrEmpty(newFile) ?
                series.CoverImage.Replace(Path.GetExtension(series.CoverImage), encodeFormat.GetExtension()) : Path.GetFileName(newFile);

            _unitOfWork.SeriesRepository.Update(series);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Updated));
            count++;
        }

        foreach (var series in seriesCovers)
        {
            if (string.IsNullOrEmpty(series.CoverImage)) continue;
            series.CoverImage = series.GetCoverImage();
            _unitOfWork.SeriesRepository.Update(series);
            await _unitOfWork.CommitAsync();
        }

        // Get all volumes and remap their covers

        // Get all series and remap their covers

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(1F, ProgressEventType.Ended));

        _logger.LogInformation("[MediaConversionService] Converted covers to {Format}", encodeFormat);
    }

    private async Task CoverAllFaviconsToEncoding()
    {
        var encodeFormat =
            (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            _logger.LogError("Cannot convert media to PNG");
            return;
        }

        _logger.LogInformation("[MediaConversionService] Starting conversion of favicons to {Format}", encodeFormat);
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(0F, ProgressEventType.Started));
        var pngFavicons = _directoryService.GetFiles(_directoryService.FaviconDirectory)
            .Where(b => !b.EndsWith(encodeFormat.GetExtension())).
            ToList();

        var count = 1F;
        foreach (var file in pngFavicons)
        {
            await SaveAsEncodingFormat(_directoryService.FaviconDirectory, _directoryService.FileSystem.FileInfo.New(file).Name, _directoryService.FaviconDirectory,
                encodeFormat);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertBookmarksProgressEvent(count / pngFavicons.Count, ProgressEventType.Updated));
            count++;
        }


        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(1F, ProgressEventType.Ended));

        _logger.LogInformation("[MediaConversionService] Converted favicons to {Format}", encodeFormat);
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
        var fullSourcePath = _directoryService.FileSystem.Path.Join(imageDirectory, filename);
        var fullTargetDirectory = fullSourcePath.Replace(new FileInfo(filename).Name, string.Empty);

        var newFilename = string.Empty;
        _logger.LogDebug("Converting {Source} image into {Encoding} at {Target}", fullSourcePath, encodeFormat, fullTargetDirectory);

        if (!File.Exists(fullSourcePath))
        {
            _logger.LogError("Requested to convert {File} but it doesn't exist", fullSourcePath);
            return newFilename;
        }

        try
        {
            // Convert target file to format then delete original target file
            try
            {
                var targetFile = await _imageService.ConvertToEncodingFormat(fullSourcePath, fullTargetDirectory, encodeFormat);
                var targetName = new FileInfo(targetFile).Name;
                newFilename = Path.Join(targetFolder, targetName);
                _directoryService.DeleteFiles(new[] {fullSourcePath});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not convert image {FilePath} to {Format}", filename, encodeFormat);
                newFilename = filename;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not convert image to {Format}", encodeFormat);
        }

        return newFilename;
    }

}
