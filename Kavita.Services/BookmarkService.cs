using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;


public class BookmarkService(
    ILogger<BookmarkService> logger,
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IMediaConversionService mediaConversionService)
    : IBookmarkService
{
    public const string Name = "BookmarkService";

    /// <summary>
    /// Deletes the files associated with the list of Bookmarks passed. Will clean up empty folders.
    /// </summary>
    /// <param name="bookmarks"></param>
    /// <param name="ct"></param>
    public async Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark?> bookmarks, CancellationToken ct = default)
    {
        var bookmarkDirectory =
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory, ct)).Value;

        var bookmarkFilesToDelete = bookmarks
            .Where(b => b != null)
            .Select(b => Parser.NormalizePath(
                directoryService.FileSystem.Path.Join(bookmarkDirectory, b!.FileName)))
            .ToList();

        if (bookmarkFilesToDelete.Count == 0) return;

        directoryService.DeleteFiles(bookmarkFilesToDelete);

        // Delete any leftover folders
        foreach (var directory in directoryService.FileSystem.Directory.GetDirectories(bookmarkDirectory, string.Empty, SearchOption.AllDirectories))
        {
            if (directoryService.FileSystem.Directory.GetFiles(directory, "", SearchOption.AllDirectories).Length == 0 &&
                directoryService.FileSystem.Directory.GetDirectories(directory).Length == 0)
            {
                directoryService.FileSystem.Directory.Delete(directory, false);
            }
        }
    }

    /// <summary>
    /// This is a job that runs after a bookmark is saved
    /// </summary>
    /// <remarks>This must be public</remarks>
    public async Task ConvertBookmarkToEncoding(int bookmarkId, CancellationToken ct = default)
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

        // Validate the bookmark still exists
        var bookmark = await unitOfWork.UserRepository.GetBookmarkAsync(bookmarkId, ct);
        if (bookmark == null) return;

        // Validate the bookmark isn't already in target format
        if (bookmark.FileName.EndsWith(encodeFormat.GetExtension()))
        {
            // Nothing to ddo
            return;
        }

        bookmark.FileName = await mediaConversionService.SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
            BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId), encodeFormat);
        unitOfWork.UserRepository.Update(bookmark);

        await unitOfWork.CommitAsync();
    }


    /// <summary>
    /// Creates a new entry in the AppUserBookmarks and copies an image to BookmarkDirectory.
    /// </summary>
    /// <param name="userWithBookmarks">An AppUser object with Bookmarks populated</param>
    /// <param name="bookmarkDto"></param>
    /// <param name="imageToBookmark">Full path to the cached image that is going to be copied</param>
    /// <param name="ct"></param>
    /// <returns>If the save to DB and copy was successful</returns>
    public async Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark,
        CancellationToken ct = default)
    {
        if (userWithBookmarks?.Bookmarks == null)
        {
            throw new KavitaException("Bookmarks cannot be null!");
        }

        try
        {
            var userBookmark = userWithBookmarks.Bookmarks
                .SingleOrDefault(b => b.Page == bookmarkDto.Page && b.ChapterId == bookmarkDto.ChapterId && b.ImageOffset == bookmarkDto.ImageOffset);
            if (userBookmark != null)
            {
                logger.LogError("Bookmark already exists for Series {SeriesId}, Volume {VolumeId}, Chapter {ChapterId}, Page {PageNum}", bookmarkDto.SeriesId, bookmarkDto.VolumeId, bookmarkDto.ChapterId, bookmarkDto.Page);
                return true;
            }

            var fileInfo = directoryService.FileSystem.FileInfo.New(imageToBookmark);
            var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            var targetFolderStem = BookmarkStem(userWithBookmarks.Id, bookmarkDto.SeriesId, bookmarkDto.ChapterId);
            var targetFilepath = Path.Join(settings.BookmarksDirectory, targetFolderStem);

            var bookmark = new AppUserBookmark()
            {
                Page = bookmarkDto.Page,
                VolumeId = bookmarkDto.VolumeId,
                SeriesId = bookmarkDto.SeriesId,
                ChapterId = bookmarkDto.ChapterId,
                FileName = Path.Join(targetFolderStem, fileInfo.Name),
                ImageOffset = bookmarkDto.ImageOffset,
                XPath = bookmarkDto.XPath,
                ChapterTitle = bookmarkDto.ChapterTitle,
                AppUserId = userWithBookmarks.Id
            };

            directoryService.CopyFileToDirectory(imageToBookmark, targetFilepath);

            unitOfWork.UserRepository.Add(bookmark);
            await unitOfWork.CommitAsync(ct);

            if (settings.EncodeMediaAs != EncodeFormat.PNG)
            {
                // Enqueue a task to convert the bookmark to webP
                BackgroundJob.Enqueue(() => ConvertBookmarkToEncoding(bookmark.Id));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when saving bookmark");
           await unitOfWork.RollbackAsync(ct);
           return false;
        }

        return true;
    }

    /// <summary>
    /// Removes the Bookmark entity and the file from BookmarkDirectory
    /// </summary>
    /// <param name="userWithBookmarks"></param>
    /// <param name="bookmarkDto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto,
        CancellationToken ct = default)
    {
        var bookmarkToDelete = userWithBookmarks.Bookmarks.FirstOrDefault(x =>
            x.ChapterId == bookmarkDto.ChapterId && x.Page == bookmarkDto.Page && x.ImageOffset == bookmarkDto.ImageOffset);
        try
        {
            if (bookmarkToDelete != null)
            {
                unitOfWork.UserRepository.Delete(bookmarkToDelete);
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception)
        {
            return false;
        }

        await DeleteBookmarkFiles([bookmarkToDelete], ct);
        return true;
    }

    public async Task<IEnumerable<string>> GetBookmarkFilesById(IEnumerable<int> bookmarkIds,
        CancellationToken ct = default)
    {
        var bookmarkDirectory =
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory, ct)).Value;

        var bookmarks = await unitOfWork.UserRepository.GetAllBookmarksByIds(bookmarkIds.ToList(), ct);

        return bookmarks
            .Select(b => Parser.NormalizePath(directoryService.FileSystem.Path.Join(bookmarkDirectory,
                b.FileName)));
    }

    public static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
