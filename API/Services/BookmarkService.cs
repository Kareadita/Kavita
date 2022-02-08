using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IBookmarkService
{
    Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark> bookmarks);
    Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark);
    Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto);
}

public class BookmarkService : IBookmarkService
{
    private readonly ILogger<BookmarkService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;

    public BookmarkService(ILogger<BookmarkService> logger, IUnitOfWork unitOfWork, IDirectoryService directoryService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
    }

    /// <summary>
    /// Deletes the files associated with the list of Bookmarks passed. Will clean up empty folders.
    /// </summary>
    /// <param name="bookmarks"></param>
    public async Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark> bookmarks)
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

        var bookmarkFilesToDelete = bookmarks.Select(b => Parser.Parser.NormalizePath(
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

            var fileInfo = new FileInfo(imageToBookmark);
            var bookmarkDirectory =
                (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
            var targetFolderStem = BookmarkStem(userWithBookmarks.Id, bookmarkDto.SeriesId, bookmarkDto.ChapterId);
            var targetFilepath = Path.Join(bookmarkDirectory, targetFolderStem);

            userWithBookmarks.Bookmarks ??= new List<AppUserBookmark>();
            userWithBookmarks.Bookmarks.Add(new AppUserBookmark()
            {
                Page = bookmarkDto.Page,
                VolumeId = bookmarkDto.VolumeId,
                SeriesId = bookmarkDto.SeriesId,
                ChapterId = bookmarkDto.ChapterId,
                FileName = Path.Join(targetFolderStem, fileInfo.Name)
            });
            _directoryService.CopyFileToDirectory(imageToBookmark, targetFilepath);
            _unitOfWork.UserRepository.Update(userWithBookmarks);
            await _unitOfWork.CommitAsync();
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

    private static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
