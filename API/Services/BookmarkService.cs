using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IBookmarkService
{
    Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark> bookmarks);
    Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark);
    Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto);
    Task<IEnumerable<string>> GetBookmarkFilesById(IEnumerable<int> bookmarkIds);
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllBookmarkToEncoding();
    Task ConvertAllCoversToEncoding();
}

public class BookmarkService : IBookmarkService
{
    public const string Name = "BookmarkService";
    private readonly ILogger<BookmarkService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IImageService _imageService;
    private readonly IEventHub _eventHub;

    public BookmarkService(ILogger<BookmarkService> logger, IUnitOfWork unitOfWork,
        IDirectoryService directoryService, IImageService imageService, IEventHub eventHub)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _imageService = imageService;
        _eventHub = eventHub;
    }

    /// <summary>
    /// Deletes the files associated with the list of Bookmarks passed. Will clean up empty folders.
    /// </summary>
    /// <param name="bookmarks"></param>
    public async Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark?> bookmarks)
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

        var bookmarkFilesToDelete = bookmarks
            .Where(b => b != null)
            .Select(b => Tasks.Scanner.Parser.Parser.NormalizePath(
                _directoryService.FileSystem.Path.Join(bookmarkDirectory, b!.FileName)))
            .ToList();

        if (bookmarkFilesToDelete.Count == 0) return;

        _directoryService.DeleteFiles(bookmarkFilesToDelete);

        // Delete any leftover folders
        foreach (var directory in _directoryService.FileSystem.Directory.GetDirectories(bookmarkDirectory, string.Empty, SearchOption.AllDirectories))
        {
            if (_directoryService.FileSystem.Directory.GetFiles(directory, "", SearchOption.AllDirectories).Length == 0 &&
                _directoryService.FileSystem.Directory.GetDirectories(directory).Length == 0)
            {
                _directoryService.FileSystem.Directory.Delete(directory, false);
            }
        }
    }

    /// <summary>
    /// This is a job that runs after a bookmark is saved
    /// </summary>
    /// <remarks>This must be public</remarks>
    public async Task ConvertBookmarkToEncoding(int bookmarkId)
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

        // Validate the bookmark still exists
        var bookmark = await _unitOfWork.UserRepository.GetBookmarkAsync(bookmarkId);
        if (bookmark == null) return;

        bookmark.FileName = await SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
            BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId), encodeFormat);
        _unitOfWork.UserRepository.Update(bookmark);

        await _unitOfWork.CommitAsync();
    }


    /// <summary>
    /// Creates a new entry in the AppUserBookmarks and copies an image to BookmarkDirectory.
    /// </summary>
    /// <param name="userWithBookmarks">An AppUser object with Bookmarks populated</param>
    /// <param name="bookmarkDto"></param>
    /// <param name="imageToBookmark">Full path to the cached image that is going to be copied</param>
    /// <returns>If the save to DB and copy was successful</returns>
    public async Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark)
    {
        if (userWithBookmarks == null || userWithBookmarks.Bookmarks == null) return false;
        try
        {
            var userBookmark = userWithBookmarks.Bookmarks.SingleOrDefault(b => b.Page == bookmarkDto.Page && b.ChapterId == bookmarkDto.ChapterId);
            if (userBookmark != null)
            {
                _logger.LogError("Bookmark already exists for Series {SeriesId}, Volume {VolumeId}, Chapter {ChapterId}, Page {PageNum}", bookmarkDto.SeriesId, bookmarkDto.VolumeId, bookmarkDto.ChapterId, bookmarkDto.Page);
                return true;
            }

            var fileInfo = _directoryService.FileSystem.FileInfo.New(imageToBookmark);
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            var targetFolderStem = BookmarkStem(userWithBookmarks.Id, bookmarkDto.SeriesId, bookmarkDto.ChapterId);
            var targetFilepath = Path.Join(settings.BookmarksDirectory, targetFolderStem);

            var bookmark = new AppUserBookmark()
            {
                Page = bookmarkDto.Page,
                VolumeId = bookmarkDto.VolumeId,
                SeriesId = bookmarkDto.SeriesId,
                ChapterId = bookmarkDto.ChapterId,
                FileName = Path.Join(targetFolderStem, fileInfo.Name),
                AppUserId = userWithBookmarks.Id
            };

            _directoryService.CopyFileToDirectory(imageToBookmark, targetFilepath);

            _unitOfWork.UserRepository.Add(bookmark);
            await _unitOfWork.CommitAsync();

            if (settings.EncodeMediaAs == EncodeFormat.WEBP)
            {
                // Enqueue a task to convert the bookmark to webP
                BackgroundJob.Enqueue(() => ConvertBookmarkToEncoding(bookmark.Id));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when saving bookmark");
           await _unitOfWork.RollbackAsync();
           return false;
        }

        return true;
    }

    /// <summary>
    /// Removes the Bookmark entity and the file from BookmarkDirectory
    /// </summary>
    /// <param name="userWithBookmarks"></param>
    /// <param name="bookmarkDto"></param>
    /// <returns></returns>
    public async Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto)
    {
        var bookmarkToDelete = userWithBookmarks.Bookmarks.SingleOrDefault(x =>
            x.ChapterId == bookmarkDto.ChapterId && x.Page == bookmarkDto.Page);
        try
        {
            if (bookmarkToDelete != null)
            {
                _unitOfWork.UserRepository.Delete(bookmarkToDelete);
            }

            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            return false;
        }

        await DeleteBookmarkFiles(new[] {bookmarkToDelete});
        return true;
    }

    public async Task<IEnumerable<string>> GetBookmarkFilesById(IEnumerable<int> bookmarkIds)
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

        var bookmarks = await _unitOfWork.UserRepository.GetAllBookmarksByIds(bookmarkIds.ToList());
        return bookmarks
            .Select(b => Tasks.Scanner.Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(bookmarkDirectory,
                b.FileName)));
    }

    /// <summary>
    /// This is a long-running job that will convert all bookmarks into WebP. Do not invoke anyway except via Hangfire.
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
            .Where(b => !b.FileName.EndsWith(".webp")).ToList();

        var count = 1F;
        foreach (var bookmark in bookmarks)
        {
            bookmark.FileName = await SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
                BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId), encodeFormat);
            _unitOfWork.UserRepository.Update(bookmark);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertBookmarksProgressEvent(count / bookmarks.Count, ProgressEventType.Started));
            count++;
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(1F, ProgressEventType.Ended));

        _logger.LogInformation("[BookmarkService] Converted bookmarks to WebP");
    }

    /// <summary>
    /// This is a long-running job that will convert all covers into WebP. Do not invoke anyway except via Hangfire.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllCoversToEncoding()
    {
        _logger.LogInformation("[BookmarkService] Starting conversion of all covers to webp");
        var coverDirectory = _directoryService.CoverImageDirectory;
        var encodeFormat =
            (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        if (encodeFormat == EncodeFormat.PNG)
        {
            _logger.LogError("Cannot convert media to PNG");
            return;
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(0F, ProgressEventType.Started));

        var chapterCovers = await _unitOfWork.ChapterRepository.GetAllChaptersWithCoversInDifferentEncoding(encodeFormat);
        var seriesCovers = await _unitOfWork.SeriesRepository.GetAllWithWithCoversInDifferentEncoding(encodeFormat);

        var readingListCovers = await _unitOfWork.ReadingListRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);
        var libraryCovers = await _unitOfWork.LibraryRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);
        var collectionCovers = await _unitOfWork.CollectionTagRepository.GetAllWithCoversInDifferentEncoding(encodeFormat);

        var totalCount = chapterCovers.Count + seriesCovers.Count + readingListCovers.Count +
                         libraryCovers.Count + collectionCovers.Count;

        var count = 1F;
        _logger.LogInformation("[BookmarkService] Starting conversion of chapters");
        foreach (var chapter in chapterCovers)
        {
            if (string.IsNullOrEmpty(chapter.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, chapter.CoverImage, coverDirectory, encodeFormat);
            chapter.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.ChapterRepository.Update(chapter);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Started));
            count++;
        }

        _logger.LogInformation("[BookmarkService] Starting conversion of series");
        foreach (var series in seriesCovers)
        {
            if (string.IsNullOrEmpty(series.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, series.CoverImage, coverDirectory, encodeFormat);
            series.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.SeriesRepository.Update(series);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Started));
            count++;
        }

        _logger.LogInformation("[BookmarkService] Starting conversion of libraries");
        foreach (var library in libraryCovers)
        {
            if (string.IsNullOrEmpty(library.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, library.CoverImage, coverDirectory, encodeFormat);
            library.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.LibraryRepository.Update(library);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Started));
            count++;
        }

        _logger.LogInformation("[BookmarkService] Starting conversion of reading lists");
        foreach (var readingList in readingListCovers)
        {
            if (string.IsNullOrEmpty(readingList.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, readingList.CoverImage, coverDirectory, encodeFormat);
            readingList.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.ReadingListRepository.Update(readingList);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Started));
            count++;
        }

        _logger.LogInformation("[BookmarkService] Starting conversion of collections");
        foreach (var collection in collectionCovers)
        {
            if (string.IsNullOrEmpty(collection.CoverImage)) continue;

            var newFile = await SaveAsEncodingFormat(coverDirectory, collection.CoverImage, coverDirectory, encodeFormat);
            collection.CoverImage = Path.GetFileName(newFile);
            _unitOfWork.CollectionTagRepository.Update(collection);
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertCoverProgressEvent(count / totalCount, ProgressEventType.Started));
            count++;
        }

        // Now null out all series and volumes that aren't webp or custom
        var nonCustomOrConvertedVolumeCovers = await _unitOfWork.VolumeRepository.GetAllWithNonWebPCovers();
        foreach (var volume in nonCustomOrConvertedVolumeCovers)
        {
            if (string.IsNullOrEmpty(volume.CoverImage)) continue;
            volume.CoverImage = null; // We null it out so when we call Refresh Metadata it will auto update from first chapter
            _unitOfWork.VolumeRepository.Update(volume);
            await _unitOfWork.CommitAsync();
        }

        var nonCustomOrConvertedSeriesCovers = await _unitOfWork.SeriesRepository.GetAllWithWithCoversInDifferentEncoding(encodeFormat, false);
        foreach (var series in nonCustomOrConvertedSeriesCovers)
        {
            if (string.IsNullOrEmpty(series.CoverImage)) continue;
            series.CoverImage = null; // We null it out so when we call Refresh Metadata it will auto update from first chapter
            _unitOfWork.SeriesRepository.Update(series);
            await _unitOfWork.CommitAsync();
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(1F, ProgressEventType.Ended));

        _logger.LogInformation("[BookmarkService] Converted covers to WebP");
    }


    /// <summary>
    /// Converts an image file, deletes original and returns the new path back
    /// </summary>
    /// <param name="imageDirectory">Full Path to where files are stored</param>
    /// <param name="filename">The file to convert</param>
    /// <param name="targetFolder">Full path to where files should be stored or any stem</param>
    /// <returns></returns>
    public async Task<string> SaveAsEncodingFormat(string imageDirectory, string filename, string targetFolder, EncodeFormat encodeFormat)
    {
        // This must be Public as it's used in via Hangfire as a background task
        var fullSourcePath = _directoryService.FileSystem.Path.Join(imageDirectory, filename);
        var fullTargetDirectory = fullSourcePath.Replace(new FileInfo(filename).Name, string.Empty);

        var newFilename = string.Empty;
        _logger.LogDebug("Converting {Source} image into WebP at {Target}", fullSourcePath, fullTargetDirectory);

        try
        {
            // Convert target file to format then delete original target file and update bookmark

            try
            {
                var targetFile = await _imageService.ConvertToEncodingFormat(fullSourcePath, fullTargetDirectory, encodeFormat);
                var targetName = new FileInfo(targetFile).Name;
                newFilename = Path.Join(targetFolder, targetName);
                _directoryService.DeleteFiles(new[] {fullSourcePath});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not convert image {FilePath}", filename);
                newFilename = filename;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not convert image to {Format}", encodeFormat);
        }

        return newFilename;
    }

    private static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
