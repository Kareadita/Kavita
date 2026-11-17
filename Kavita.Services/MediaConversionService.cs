using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class MediaConversionService(
    IUnitOfWork unitOfWork,
    IImageService imageService,
    IEventHub eventHub,
    IDirectoryService directoryService,
    IImageFileService imageFileService,
    ILogger<MediaConversionService> logger)
    : IMediaConversionService
{
    /// <summary>
    /// This is a long-running job that will convert all bookmarks into a format. Do not invoke anyway except via Hangfire.
    /// </summary>
    /// <param name="ct"></param>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task ConvertAllBookmarkToEncoding(CancellationToken ct = default)
    {
        var bookmarkDirectory = await imageFileService.GetImageDirectoryAsync(ImageKind.Bookmark, ct);
        var encodeFormat =
            (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).EncodeMediaAs;

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(0F, ProgressEventType.Started), ct: ct);

        var bookmarks = (await unitOfWork.UserRepository.GetAllBookmarksAsync(ct))
            .Where(b => !b.FileName.EndsWith(encodeFormat.GetExtension())).ToList();

        var count = 1F;
        foreach (var bookmark in bookmarks)
        {
            bookmark.FileName = await SaveAsEncodingFormat(bookmarkDirectory, bookmark.FileName,
                BookmarkService.BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId), encodeFormat);

            unitOfWork.UserRepository.Update(bookmark);

            await unitOfWork.CommitAsync(ct);
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ConvertBookmarksProgressEvent(count / bookmarks.Count, ProgressEventType.Updated), ct: ct);

            count++;
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertBookmarksProgressEvent(1F, ProgressEventType.Ended), ct: ct);

        logger.LogInformation("[MediaConversionService] Converted bookmarks to {Format}", encodeFormat);
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
        var fullSourcePath = directoryService.FileSystem.Path.Join(imageDirectory, filename);
        var fullTargetDirectory = fullSourcePath.Replace(new FileInfo(filename).Name, string.Empty);

        var newFilename = string.Empty;
        logger.LogDebug("Converting {Source} image into {Encoding} at {Target}", fullSourcePath, encodeFormat, fullTargetDirectory);

        if (!File.Exists(fullSourcePath))
        {
            logger.LogError("Requested to convert {File} but it doesn't exist", fullSourcePath);
            return newFilename;
        }

        // Converting in place would write over the source and then delete it
        if (Path.GetExtension(filename).Equals(encodeFormat.GetExtension(), StringComparison.OrdinalIgnoreCase))
        {
            return Path.Join(targetFolder, new FileInfo(filename).Name);
        }

        try
        {
            // Convert target file to format then delete original target file
            try
            {
                var targetFile = await imageService.ConvertToEncodingFormat(fullSourcePath, fullTargetDirectory, encodeFormat);
                var targetName = new FileInfo(targetFile).Name;
                newFilename = Path.Join(targetFolder, targetName);
                directoryService.DeleteFiles([fullSourcePath]);
                if (File.Exists(fullSourcePath))
                {
                    logger.LogWarning("Converted {File} to {Format} but could not delete the original", fullSourcePath, encodeFormat);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not convert image {FilePath} to {Format}", filename, encodeFormat);
                newFilename = filename;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not convert image to {Format}", encodeFormat);
        }

        return newFilename;
    }

}
