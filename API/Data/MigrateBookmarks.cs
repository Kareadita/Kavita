using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// Responsible to migrate existing bookmarks to files
/// </summary>
public static class MigrateBookmarks
{
    private static Version _versionBookmarksChanged = new Version(0, 4, 9, 27);
    /// <summary>
    /// This will migrate existing bookmarks to bookmark folder based
    /// </summary>
    /// <remarks>Bookmark directory is configurable. This will always use the default bookmark directory.</remarks>
    /// <param name="directoryService"></param>
    /// <returns></returns>
    public static void Migrate(IDirectoryService directoryService, DbContext context, ILogger logger)
    {
        // NOTE: This migration can be run after startup technically, which will let us use all our services. I can just kick off a task immediately.
        //var settingsRepository = serviceProvider.GetRequiredService<ISettingsRepository>();
        var existingVersion = GetSetting(context, ServerSettingKey.InstallVersion);
        var bookmarkDirectory = GetSetting(context, ServerSettingKey.BookmarkDirectory);
        if (string.IsNullOrEmpty(bookmarkDirectory))
        {
            bookmarkDirectory = directoryService.BookmarkDirectory;
        }

        if (string.IsNullOrEmpty(existingVersion) || Version.Parse(existingVersion) >= _versionBookmarksChanged
                                                  || directoryService.IsDirectoryEmpty(bookmarkDirectory))
        {
            logger.LogInformation("Bookmark migration is needed");
            // var allBookmarks = (await unitOfWork.UserRepository.GetAllBookmarksAsync()).ToList();
            //
            //
            // var uniqueChapterIds = allBookmarks.Select(b => b.ChapterId).Distinct().ToList();
            // foreach (var chapterId in uniqueChapterIds)
            // {
            //     var chapterPages = allBookmarks.Where(b => b.ChapterId == chapterId)
            //         .Select(b => b.Page).ToList();
            //     var mangaFiles = await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
            // }
            //
            //
            // foreach (var bookmark in allBookmarks)
            // {
            //     // TODO: Investigate a way to keep this logic consistent between code
            //     var path = string.Empty;
            //     // directoryService.CopyFileToDirectory(path, directoryService.FileSystem.Path.Join(bookmarkDirectory,
            //     //     ReaderService.FormatBookmarkPage(bookmarkDirectory, bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId)));
            //
            // }

        }
        else
        {
            return;
        }



    }



    private static string GetSetting(DbContext context, ServerSettingKey key)
    {
        var val = (int) key;
        Console.WriteLine($"Select Value from ServerSetting Where Key = {val}");
        return SqlHelper.RawSqlQuery(context, $"Select Value from ServerSetting Where Key = {val}" , x => x[0].ToString()).FirstOrDefault();
    }

    // private void GetFiles(IList<AppUserBookmark> bookmarks, IDirectoryService directoryService)
    // {
    //     var uniqueChapterIds = bookmarks.Select(b => b.ChapterId).Distinct().ToList();
    //
    //         // TODO: Rewrite this logic so bookmark download just zips up the files in bookmarks/ directory
    //         foreach (var chapterId in uniqueChapterIds)
    //         {
    //             var chapterExtractPath = directoryService.FileSystem.Path.Join(fullExtractPath, $"{series.Id}_bookmark_{chapterId}");
    //             var chapterPages = downloadBookmarkDto.Bookmarks.Where(b => b.ChapterId == chapterId)
    //                 .Select(b => b.Page).ToList();
    //             var mangaFiles = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
    //             switch (series.Format)
    //             {
    //                 case MangaFormat.Image:
    //                     _directoryService.ExistOrCreate(chapterExtractPath);
    //                     _directoryService.CopyFilesToDirectory(mangaFiles.Select(f => f.FilePath), chapterExtractPath, $"{chapterId}_");
    //                     break;
    //                 case MangaFormat.Archive:
    //                 case MangaFormat.Pdf:
    //                     _cacheService.ExtractChapterFiles(chapterExtractPath, mangaFiles.ToList());
    //                     var originalFiles = _directoryService.GetFilesWithExtension(chapterExtractPath,
    //                         Parser.Parser.ImageFileExtensions);
    //                     directoryService.CopyFilesToDirectory(originalFiles, chapterExtractPath, $"{chapterId}_");
    //                     directoryService.DeleteFiles(originalFiles);
    //                     break;
    //                 case MangaFormat.Epub:
    //                     return BadRequest("Series is not in a valid format.");
    //                 default:
    //                     return BadRequest("Series is not in a valid format. Please rescan series and try again.");
    //             }
    //
    //             var files = directoryService.GetFilesWithExtension(chapterExtractPath, Parser.Parser.ImageFileExtensions);
    //             // Filter out images that aren't in bookmarks
    //             Array.Sort(files, _numericComparer);
    //             totalFilePaths.AddRange(files.Where((_, i) => chapterPages.Contains(i)));
    //         }
    // }
}
