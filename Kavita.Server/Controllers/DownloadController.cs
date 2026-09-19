using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Downloads;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Services.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;


/// <summary>
/// All APIs related to downloading entities from the system. Requires Download Role or Admin Role.
/// </summary>
[Authorize(PolicyGroups.DownloadPolicy)]
public class DownloadController(
    IUnitOfWork unitOfWork,
    IArchiveService archiveService,
    IDownloadService downloadService,
    IEventHub eventHub,
    ILogger<DownloadController> logger,
    IBookmarkService bookmarkService,
    ILocalizationService localizationService)
    : BaseApiController
{
    private const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// For a given volume, return the size in bytes
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet("volume-size")]
    public async Task<ActionResult<long>> GetVolumeSize(int volumeId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.VolumeRepository.GetFilesizeAsync(volumeId, ct));
    }

    /// <summary>
    /// For a set of volumes, return the size in bytes
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("bulk-volume-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetBulkVolumeSize(BulkVolumeSizeRequest request)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.VolumeRepository.GetFilesizesAsync(UserId, request.VolumeIds, ct));
    }

    /// <summary>
    /// For a given chapter, return the size in bytes
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter-size")]
    public async Task<ActionResult<long>> GetChapterSize(int chapterId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.ChapterRepository.GetFilesizeAsync(chapterId, ct));
    }


    /// <summary>
    /// For a set of chapters, return the size in bytes
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("bulk-chapter-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetChapterSizeInBulk(BulkChapterSizeRequest request)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.ChapterRepository.GetFilesizesAsync(UserId, request.ChapterIds, ct));
    }

    /// <summary>
    /// For a series, return the size in bytes
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("series-size")]
    public async Task<ActionResult<long>> GetSeriesSize(int seriesId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.SeriesRepository.GetFilesizeAsync(seriesId, ct));
    }

    /// <summary>
    /// Returns the filesize for all items of a reading list that the requesting user has access to
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [ReadingListAccess]
    [HttpGet("readinglist-size")]
    public async Task<ActionResult<long>> GetReadingListSize(int readingListId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.ReadingListRepository.GetFilesizeAsync(readingListId, UserId, ct));
    }

    /// <summary>
    /// Returns the mapping of readinglist -> size
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("bulk-readinglist-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetBulkReadingListSize(BulkReadingListSizeRequest request)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.ReadingListRepository.GetFilesizesAsync(request.ReadingListIds, UserId, ct));
    }

    /// <summary>
    /// For a set of series, return the size in bytes
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("bulk-series-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetBulkSeriesSize(BulkSeriesSizeRequest request)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.SeriesRepository.GetFilesizesAsync(UserId, request.SeriesIds, ct));
    }


    /// <summary>
    /// Downloads all chapters within a volume. If the chapters are multiple zips, they will all be zipped up.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="correlationId">Only for UI</param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet("volume")]
    [Authorize(PolicyGroups.DownloadPolicy)]
    public async Task<ActionResult> DownloadVolume(int volumeId, [FromQuery] string? correlationId = null)
    {
        var ct = HttpContext.RequestAborted;
        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId, ct: ct);
        if (volume == null) return BadRequest(await localizationService.TranslateAsync(UserId, "volume-doesnt-exist"));

        var files = await unitOfWork.VolumeRepository.GetFilesForVolume(volumeId, ct);
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId, ct: ct);

        try
        {
            return await DownloadFiles(files, $"download_{Username!}_v{volumeId}", $"{series!.Name} - Volume {volume.Name}.zip", correlationId, ct);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private PhysicalFileResult GetFirstFileDownload(IEnumerable<MangaFile> files)
    {
        var (zipFile, contentType, fileDownloadName) = downloadService.GetFirstFileDownload(files);
        return PhysicalFile(zipFile, contentType, fileDownloadName, true);
    }

    /// <summary>
    /// Returns the zip for a single chapter. If the chapter contains multiple files, they will be zipped.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter")]
    public async Task<ActionResult> DownloadChapter(int chapterId, [FromQuery] string? correlationId = null)
    {
        var ct = HttpContext.RequestAborted;
        var files = await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId, ct);
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ct: ct);
        if (chapter == null) return BadRequest(await localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId, ct: ct);
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume!.SeriesId, ct: ct);

        try
        {
            return await DownloadFiles(files,
                $"download_{Username!}_c{chapterId}",
                $"{series!.Name} - Chapter {chapter.GetNumberTitle()}.zip",
                correlationId, ct);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    private async Task<ActionResult> DownloadFiles(ICollection<MangaFile> files, string tempFolder, string downloadName, string? correlationId = null, CancellationToken ct = default)
    {
        var username = Username!;
        var filename = Path.GetFileNameWithoutExtension(downloadName);
        try
        {
            await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                MessageFactory.DownloadProgressEvent(username,
                    filename, $"Downloading {filename}", 0F, "started", correlationId), ct: ct);

            if (files.Count == 1 && files.First().Format != MangaFormat.Image)
            {
                // Emit "ended" after the response is fully sent to the client
                HttpContext.Response.OnCompleted(async () =>
                {
                    await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                        MessageFactory.DownloadProgressEvent(username,
                            filename, "Download Complete", 1F, "ended", correlationId), ct: ct);
                });
                return GetFirstFileDownload(files);
            }

            var filePath = archiveService.CreateZipFromFoldersForDownload(files.Select(c => c.FilePath).ToList(), tempFolder, ProgressCallback);

            await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                MessageFactory.DownloadProgressEvent(username,
                    filename, "Download Complete", 1F, "ended", correlationId), ct: ct);

            return PhysicalFile(filePath, DefaultContentType, Uri.EscapeDataString(downloadName), true);

            async Task ProgressCallback(Tuple<string, float> progressInfo)
            {
                await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                    MessageFactory.DownloadProgressEvent(username, filename, $"Processing {Path.GetFileNameWithoutExtension(progressInfo.Item1)}",
                        Math.Clamp(progressInfo.Item2, 0F, 1F), correlationId), ct: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when trying to download files");
            await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                MessageFactory.DownloadProgressEvent(Username!,
                    filename, "Download Complete", 1F, "ended", correlationId), ct: ct);
            throw;
        }
    }

    [SeriesAccess]
    [HttpGet("series")]
    [Authorize(PolicyGroups.DownloadPolicy)]
    public async Task<ActionResult> DownloadSeries(int seriesId, [FromQuery] string? correlationId = null)
    {
        var ct = HttpContext.RequestAborted;
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ct: ct);
        if (series == null) return BadRequest("Invalid Series");

        var files = await unitOfWork.SeriesRepository.GetFilesForSeriesAsync(seriesId, ct);
        try
        {
            return await DownloadFiles(files, $"download_{Username!}_s{seriesId}", $"{series.Name}.zip", correlationId, ct);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Downloads all bookmarks in a zip for
    /// </summary>
    /// <param name="downloadBookmarkDto"></param>
    /// <returns></returns>
    [HttpPost("bookmarks")]
    [Authorize(PolicyGroups.DownloadPolicy)]
    public async Task<ActionResult> DownloadBookmarkPages(DownloadBookmarkDto downloadBookmarkDto)
    {
        var ct = HttpContext.RequestAborted;
        if (downloadBookmarkDto.Bookmarks.DistinctBy(b => b.SeriesId).Count() > 1)
            return BadRequest();

        var seriesId = downloadBookmarkDto.Bookmarks.First().SeriesId;
        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, seriesId, HttpContext.RequestAborted))
            return NotFound();

        if (!downloadBookmarkDto.Bookmarks.Any()) return BadRequest(await localizationService.TranslateAsync(UserId, "bookmarks-empty"));

        var userId = UserId;
        var username = Username!;
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ct: ct);

        var files = await bookmarkService.GetBookmarkFilesById(seriesId, downloadBookmarkDto.Bookmarks.Select(b => b.Id), ct);

        var filename = $"{series!.Name} - Bookmarks.zip";

        await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
            MessageFactory.DownloadProgressEvent(username, Path.GetFileNameWithoutExtension(filename), $"Downloading {filename}",0F), ct: ct);

        var filePath =  archiveService.CreateZipForDownload(files,$"download_{userId}_{seriesId}_bookmarks");

        await eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
            MessageFactory.DownloadProgressEvent(username, Path.GetFileNameWithoutExtension(filename), $"Downloading {filename}", 1F), ct: ct);


        return PhysicalFile(filePath, DefaultContentType, Uri.EscapeDataString(filename), true);
    }

}
