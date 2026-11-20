using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SignalR;
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
    IImageFileService imageFileService,
    IEventHub eventHub)
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

    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task MoveBookmarkDirectory(string to, CancellationToken ct = default)
    {
        var fileSystem = directoryService.FileSystem;
        var setting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory, ct);
        var source = fileSystem.Path.GetFullPath(setting.Value);
        var target = fileSystem.Path.GetFullPath(to);
        if (Parser.NormalizePath(source).TrimEnd('/') == Parser.NormalizePath(target).TrimEnd('/')) return;

        directoryService.ExistOrCreate(target);

        var moved = 0;
        var skipped = new List<string>();
        if (fileSystem.Directory.Exists(source))
        {
            foreach (var file in fileSystem.Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = fileSystem.Path.Join(target, fileSystem.Path.GetRelativePath(source, file));
                if (fileSystem.File.Exists(destination))
                {
                    skipped.Add(file);
                    continue;
                }

                fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(destination)!);
                fileSystem.File.Move(file, destination);
                moved++;
            }
        }
        else
        {
            logger.LogWarning("The old bookmark folder {Folder} does not exist, nothing to move to {Target}", source, target);
        }

        setting.Value = target;
        unitOfWork.SettingsRepository.Update(setting);
        await unitOfWork.CommitAsync(ct);

        if (fileSystem.Directory.Exists(source))
        {
            DeleteEmptyFolders(source);
        }

        foreach (var file in skipped)
        {
            logger.LogWarning("Left bookmark file {File} in the old folder, {Target} already has a file of that name", file, target);
        }

        logger.LogInformation("Moved {Moved} bookmark files from {Source} to {Target}, {Skipped} left behind", moved, source, target,
            skipped.Count);
        await eventHub.SendMessageAsync(MessageFactory.Info,
            MessageFactory.BookmarkDirectoryMovedEvent(target, moved, skipped.Count), ct: ct);
    }

    /// <summary>
    /// Deletes <paramref name="folder"/> and every folder under it that holds no file
    /// </summary>
    private void DeleteEmptyFolders(string folder)
    {
        var fileSystem = directoryService.FileSystem;
        foreach (var child in fileSystem.Directory.GetDirectories(folder))
        {
            DeleteEmptyFolders(child);
        }

        if (!fileSystem.Directory.EnumerateFileSystemEntries(folder).Any())
        {
            fileSystem.Directory.Delete(folder, false);
        }
    }

    public static string BookmarkStem(int userId, int seriesId, int chapterId)
    {
        return Path.Join($"{userId}", $"{seriesId}", $"{chapterId}");
    }
}
