using System;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Entities.Enums;
using API.Services;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// Responsible to migrate existing bookmarks to files. Introduced in v0.4.9.27
/// </summary>
public static class MigrateBookmarks
{
    /// <summary>
    /// This will migrate existing bookmarks to bookmark folder based.
    /// If the bookmarks folder already exists, this will not run.
    /// </summary>
    /// <remarks>Bookmark directory is configurable. This will always use the default bookmark directory.</remarks>
    /// <param name="directoryService"></param>
    /// <param name="unitOfWork"></param>
    /// <param name="logger"></param>
    /// <param name="cacheService"></param>
    /// <returns></returns>
    public static async Task Migrate(IDirectoryService directoryService, IUnitOfWork unitOfWork,
        ILogger<Program> logger, ICacheService cacheService)
    {
        var bookmarkDirectory = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory))
            .Value;
        if (string.IsNullOrEmpty(bookmarkDirectory))
        {
            bookmarkDirectory = directoryService.BookmarkDirectory;
        }

        if (directoryService.Exists(bookmarkDirectory)) return;

        logger.LogInformation("Bookmark migration is needed....This may take some time");

        var allBookmarks = (await unitOfWork.UserRepository.GetAllBookmarksAsync()).ToList();

        var uniqueChapterIds = allBookmarks.Select(b => b.ChapterId).Distinct().ToList();
        var uniqueUserIds = allBookmarks.Select(b => b.AppUserId).Distinct().ToList();
        foreach (var userId in uniqueUserIds)
        {
            foreach (var chapterId in uniqueChapterIds)
            {
                var chapterBookmarks = allBookmarks.Where(b => b.ChapterId == chapterId).ToList();
                var chapterPages = chapterBookmarks
                    .Select(b => b.Page).ToList();
                var seriesId = chapterBookmarks
                    .Select(b => b.SeriesId).First();
                var mangaFiles = await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
                var chapterExtractPath = directoryService.FileSystem.Path.Join(directoryService.TempDirectory, $"bookmark_c{chapterId}_u{userId}_s{seriesId}");

                var numericComparer = new NumericComparer();
                if (!mangaFiles.Any()) continue;

                switch (mangaFiles.First().Format)
                {
                    case MangaFormat.Image:
                        directoryService.ExistOrCreate(chapterExtractPath);
                        directoryService.CopyFilesToDirectory(mangaFiles.Select(f => f.FilePath), chapterExtractPath);
                        break;
                    case MangaFormat.Archive:
                    case MangaFormat.Pdf:
                        cacheService.ExtractChapterFiles(chapterExtractPath, mangaFiles.ToList());
                        break;
                    case MangaFormat.Epub:
                        continue;
                    default:
                        continue;
                }

                var files = directoryService.GetFilesWithExtension(chapterExtractPath, Services.Tasks.Scanner.Parser.Parser.ImageFileExtensions);
                // Filter out images that aren't in bookmarks
                Array.Sort(files, numericComparer);
                foreach (var chapterPage in chapterPages)
                {
                    var file = files.ElementAt(chapterPage);
                    var bookmark = allBookmarks.FirstOrDefault(b =>
                        b.ChapterId == chapterId && b.SeriesId == seriesId && b.AppUserId == userId &&
                        b.Page == chapterPage);
                    if (bookmark == null) continue;

                    var filename = directoryService.FileSystem.Path.GetFileName(file);
                    var newLocation = directoryService.FileSystem.Path.Join(
                        ReaderService.FormatBookmarkFolderPath(String.Empty, userId, seriesId, chapterId),
                        filename);
                    bookmark.FileName = newLocation;
                    directoryService.CopyFileToDirectory(file,
                        ReaderService.FormatBookmarkFolderPath(bookmarkDirectory, userId, seriesId, chapterId));
                    unitOfWork.UserRepository.Update(bookmark);
                }
            }
            // Clear temp after each user to avoid too much space being eaten
            directoryService.ClearDirectory(directoryService.TempDirectory);
        }

        await unitOfWork.CommitAsync();
        // Run CleanupService as we cache a ton of files
        directoryService.ClearDirectory(directoryService.TempDirectory);

    }
}
