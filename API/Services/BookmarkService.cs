using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services;

#nullable enable

public interface IBookmarkService
{
    Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark> bookmarks);
    Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark);
    Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto);
    Task<IEnumerable<string>> GetBookmarkFilesById(IEnumerable<int> bookmarkIds);
}

public class BookmarkService : IBookmarkService
{
    public const string Name = "BookmarkService";
    private readonly ILogger<BookmarkService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IMediaConversionService _mediaConversionService;

    public BookmarkService(ILogger<BookmarkService> logger, IUnitOfWork unitOfWork,
        IDirectoryService directoryService, IMediaConversionService mediaConversionService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _mediaConversionService = mediaConversionService;
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

        // Validate the bookmark isn't already in target format
        if (bookmark.FileName.EndsWith(encodeFormat.GetExtension()))
        {
            // Nothing to ddo
            return;
        }

        bookmark.FileName = await _mediaConversionService.SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
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

            if (settings.EncodeMediaAs != EncodeFormat.PNG)
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



    public static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
