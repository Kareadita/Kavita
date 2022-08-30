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
    Task ConvertAllBookmarkToWebP();

}

public class BookmarkService : IBookmarkService
{
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
    public async Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark> bookmarks)
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

        var bookmarkFilesToDelete = bookmarks.Select(b => Tasks.Scanner.Parser.Parser.NormalizePath(
            _directoryService.FileSystem.Path.Join(bookmarkDirectory,
                b.FileName))).ToList();

        if (bookmarkFilesToDelete.Count == 0) return;

        _directoryService.DeleteFiles(bookmarkFilesToDelete);

        // Delete any leftover folders
        foreach (var directory in _directoryService.FileSystem.Directory.GetDirectories(bookmarkDirectory, "", SearchOption.AllDirectories))
        {
            if (_directoryService.FileSystem.Directory.GetFiles(directory, "", SearchOption.AllDirectories).Length == 0 &&
                _directoryService.FileSystem.Directory.GetDirectories(directory).Length == 0)
            {
                _directoryService.FileSystem.Directory.Delete(directory, false);
            }
        }
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
        try
        {
            var userBookmark =
                await _unitOfWork.UserRepository.GetBookmarkForPage(bookmarkDto.Page, bookmarkDto.ChapterId, userWithBookmarks.Id);

            if (userBookmark != null)
            {
                _logger.LogError("Bookmark already exists for Series {SeriesId}, Volume {VolumeId}, Chapter {ChapterId}, Page {PageNum}", bookmarkDto.SeriesId, bookmarkDto.VolumeId, bookmarkDto.ChapterId, bookmarkDto.Page);
                return false;
            }

            var fileInfo = _directoryService.FileSystem.FileInfo.FromFileName(imageToBookmark);
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            var targetFolderStem = BookmarkStem(userWithBookmarks.Id, bookmarkDto.SeriesId, bookmarkDto.ChapterId);
            var targetFilepath = Path.Join(settings.BookmarksDirectory, targetFolderStem);

            var bookmark = new AppUserBookmark()
            {
                Page = bookmarkDto.Page,
                VolumeId = bookmarkDto.VolumeId,
                SeriesId = bookmarkDto.SeriesId,
                ChapterId = bookmarkDto.ChapterId,
                FileName = Path.Join(targetFolderStem, fileInfo.Name)
            };

            _directoryService.CopyFileToDirectory(imageToBookmark, targetFilepath);
            userWithBookmarks.Bookmarks ??= new List<AppUserBookmark>();
            userWithBookmarks.Bookmarks.Add(bookmark);

            _unitOfWork.UserRepository.Update(userWithBookmarks);
            await _unitOfWork.CommitAsync();

            if (settings.ConvertBookmarkToWebP)
            {
                // Enqueue a task to convert the bookmark to webP
                BackgroundJob.Enqueue(() => ConvertBookmarkToWebP(bookmark.Id));
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
        if (userWithBookmarks.Bookmarks == null) return true;
        try
        {
            var bookmarkToDelete = userWithBookmarks.Bookmarks.SingleOrDefault(x =>
                x.ChapterId == bookmarkDto.ChapterId && x.AppUserId == userWithBookmarks.Id && x.Page == bookmarkDto.Page &&
                x.SeriesId == bookmarkDto.SeriesId);

            if (bookmarkToDelete != null)
            {
                await DeleteBookmarkFiles(new[] {bookmarkToDelete});
                _unitOfWork.UserRepository.Delete(bookmarkToDelete);
            }

            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            return false;
        }

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
    public async Task ConvertAllBookmarkToWebP()
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(0F, ProgressEventType.Started));
        var bookmarks = (await _unitOfWork.UserRepository.GetAllBookmarksAsync())
            .Where(b => !b.FileName.EndsWith(".webp")).ToList();

        var count = 1F;
        foreach (var bookmark in bookmarks)
        {
            await SaveBookmarkAsWebP(bookmarkDirectory, bookmark);
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
    /// This is a job that runs after a bookmark is saved
    /// </summary>
    public async Task ConvertBookmarkToWebP(int bookmarkId)
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        var convertBookmarkToWebP =
            (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).ConvertBookmarkToWebP;

        if (!convertBookmarkToWebP) return;

        // Validate the bookmark still exists
        var bookmark = await _unitOfWork.UserRepository.GetBookmarkAsync(bookmarkId);
        if (bookmark == null) return;

        await SaveBookmarkAsWebP(bookmarkDirectory, bookmark);
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Converts bookmark file, deletes original, marks bookmark as dirty. Does not commit.
    /// </summary>
    /// <param name="bookmarkDirectory"></param>
    /// <param name="bookmark"></param>
    private async Task SaveBookmarkAsWebP(string bookmarkDirectory, AppUserBookmark bookmark)
    {
        var fullSourcePath = _directoryService.FileSystem.Path.Join(bookmarkDirectory, bookmark.FileName);
        var fullTargetDirectory = fullSourcePath.Replace(new FileInfo(bookmark.FileName).Name, string.Empty);
        var targetFolderStem = BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId);

        _logger.LogDebug("Converting {Source} bookmark into WebP at {Target}", fullSourcePath, fullTargetDirectory);

        try
        {
            // Convert target file to webp then delete original target file and update bookmark

            var originalFile = bookmark.FileName;
            try
            {
                var targetFile = await _imageService.ConvertToWebP(fullSourcePath, fullTargetDirectory);
                var targetName = new FileInfo(targetFile).Name;
                bookmark.FileName = Path.Join(targetFolderStem, targetName);
                _directoryService.DeleteFiles(new[] {fullSourcePath});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not convert file {FilePath}", bookmark.FileName);
                bookmark.FileName = originalFile;
            }
            _unitOfWork.UserRepository.Update(bookmark);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not convert bookmark to WebP");
        }
    }

    private static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
