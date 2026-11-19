using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;


public class BookmarkService(
    ILogger<BookmarkService> logger,
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IImageFileService imageFileService)
    : IBookmarkService
{
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


        var bookmark = new AppUserBookmark()
        {
            Page = bookmarkDto.Page,
            VolumeId = bookmarkDto.VolumeId,
            SeriesId = bookmarkDto.SeriesId,
            ChapterId = bookmarkDto.ChapterId,
            ImageOffset = bookmarkDto.ImageOffset,
            XPath = bookmarkDto.XPath,
            ChapterTitle = bookmarkDto.ChapterTitle,
            AppUserId = userWithBookmarks.Id
        };

        try
        {

            var userBookmark = userWithBookmarks.Bookmarks
                .SingleOrDefault(b => b.Page == bookmarkDto.Page && b.ChapterId == bookmarkDto.ChapterId && b.ImageOffset == bookmarkDto.ImageOffset);
            if (userBookmark != null)
            {
                logger.LogError("Bookmark already exists for Series {SeriesId}, Volume {VolumeId}, Chapter {ChapterId}, Page {PageNum}", bookmarkDto.SeriesId, bookmarkDto.VolumeId, bookmarkDto.ChapterId, bookmarkDto.Page);
                return true;
            }

            await imageFileService.WriteBookmarkAsync(bookmark, imageToBookmark, ct);

            unitOfWork.UserRepository.Add(bookmark);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when saving bookmark");
           await unitOfWork.RollbackAsync(ct);

           // Cleanup never deletes bookmark files, so a page written before a failed commit would stay forever
           if (bookmark.ImageFile != null)
           {
               var root = await imageFileService.GetImageDirectoryAsync(ImageKind.Bookmark, ct);
               directoryService.DeleteFileQuietly(Path.Join(root, bookmark.ImageFile.FileName));
           }

           return false;
        }

        return true;
    }

    /// <summary>
    /// Removes the user's bookmark for the page, its image row and its file
    /// </summary>
    /// <param name="userWithBookmarks">An AppUser with Bookmarks and their ImageFile loaded</param>
    /// <returns>False when the commit failed</returns>
    public async Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto,
        CancellationToken ct = default)
    {
        var bookmark = userWithBookmarks.Bookmarks.FirstOrDefault(x =>
            x.ChapterId == bookmarkDto.ChapterId && x.Page == bookmarkDto.Page && x.ImageOffset == bookmarkDto.ImageOffset);
        if (bookmark == null) return true;

        return await RemoveBookmarksAsync([bookmark], ct);
    }

    public async Task<bool> RemoveBookmarksAsync(IList<AppUserBookmark> bookmarks, CancellationToken ct = default)
    {
        if (bookmarks.Count == 0) return true;

        var fileNames = bookmarks
            .Where(b => b.ImageFile != null)
            .Select(b => b.ImageFile!.FileName)
            .ToList();

        foreach (var bookmark in bookmarks)
        {
            if (bookmark.ImageFile != null)
            {
                unitOfWork.ImageFileRepository.Remove(bookmark.ImageFile);
            }

            unitOfWork.UserRepository.Delete(bookmark);
        }

        try
        {
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when removing bookmarks");
            await unitOfWork.RollbackAsync(ct);
            return false;
        }

        await DeleteBookmarkFiles(fileNames, ct);
        return true;
    }

    public async Task<IEnumerable<string>> GetBookmarkFilesById(int userId, int seriesId, IEnumerable<int> bookmarkIds,
        CancellationToken ct = default)
    {
        var bookmarkDirectory = await imageFileService.GetImageDirectoryAsync(ImageKind.Bookmark, ct);

        var bookmarks = await unitOfWork.UserRepository.GetAllBookmarksByIds(userId, seriesId, bookmarkIds.ToList(), ct);

        return bookmarks
            .Where(b => b.ImageFile != null)
            .Select(b => Parser.NormalizePath(directoryService.FileSystem.Path.Join(bookmarkDirectory,
                b.ImageFile!.FileName)));
    }

    /// <summary>
    /// Deletes the files, then each folder they leave empty up to the bookmark folder
    /// </summary>
    /// <param name="fileNames">Relative to the bookmark folder</param>
    private async Task DeleteBookmarkFiles(IList<string> fileNames, CancellationToken ct)
    {
        if (fileNames.Count == 0) return;

        var fileSystem = directoryService.FileSystem;
        var bookmarkDirectory = Parser.NormalizePath(await imageFileService.GetImageDirectoryAsync(ImageKind.Bookmark, ct))
            .TrimEnd('/');

        foreach (var fileName in fileNames)
        {
            var path = Parser.NormalizePath(fileSystem.Path.Join(bookmarkDirectory, fileName));
            directoryService.DeleteFileQuietly(path);

            var folder = Parser.NormalizePath(fileSystem.Path.GetDirectoryName(path));
            while (folder.StartsWith(bookmarkDirectory + "/") && fileSystem.Directory.Exists(folder) &&
                   !fileSystem.Directory.EnumerateFileSystemEntries(folder).Any())
            {
                fileSystem.Directory.Delete(folder, false);
                folder = Parser.NormalizePath(fileSystem.Path.GetDirectoryName(folder));
            }
        }
    }

    public static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
