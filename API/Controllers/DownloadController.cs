using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Downloads;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

/// <summary>
/// All APIs related to downloading entities from the system. Requires Download Role or Admin Role.
/// </summary>
[Authorize(PolicyGroups.DownloadPolicy)]
public class DownloadController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IArchiveService _archiveService;
    private readonly IDirectoryService _directoryService;
    private readonly IDownloadService _downloadService;
    private readonly IEventHub _eventHub;
    private readonly ILogger<DownloadController> _logger;
    private readonly IBookmarkService _bookmarkService;
    private readonly IAccountService _accountService;
    private readonly ILocalizationService _localizationService;
    private const string DefaultContentType = "application/octet-stream";

    public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService,
        IDownloadService downloadService, IEventHub eventHub, ILogger<DownloadController> logger, IBookmarkService bookmarkService,
        IAccountService accountService, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _archiveService = archiveService;
        _directoryService = directoryService;
        _downloadService = downloadService;
        _eventHub = eventHub;
        _logger = logger;
        _bookmarkService = bookmarkService;
        _accountService = accountService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// For a given volume, return the size in bytes
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("volume-size")]
    public async Task<ActionResult<long>> GetVolumeSize(int volumeId)
    {
        return Ok(await _unitOfWork.VolumeRepository.GetFilesizeForVolumeAsync(volumeId));
    }

    /// <summary>
    /// For a set of volumes, return the size in bytes
    /// </summary>
    /// <param name="volumeIds"></param>
    /// <returns></returns>
    [HttpPost("bulk-volume-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetBulkVolumeSize([FromBody] IList<int> volumeIds)
    {
        return Ok(await _unitOfWork.VolumeRepository.GetFilesizeForVolumesAsync(volumeIds));
    }

    /// <summary>
    /// For a given chapter, return the size in bytes
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-size")]
    public async Task<ActionResult<long>> GetChapterSize(int chapterId)
    {
        return Ok(await _unitOfWork.ChapterRepository.GetFilesizeForChapterAsync(chapterId));
    }

    /// <summary>
    /// For a set of chapters, return the size in bytes
    /// </summary>
    /// <param name="chapterIds"></param>
    /// <returns></returns>
    [HttpPost("bulk-chapter-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetChapterSizeInBulk([FromBody] IList<int> chapterIds)
    {
        // If there are more than 50 chapterIds, we need to break up into multiple calls
        return Ok(await _unitOfWork.ChapterRepository.GetFilesizeForChaptersAsync(chapterIds));
    }

    /// <summary>
    /// For a series, return the size in bytes
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("series-size")]
    public async Task<ActionResult<long>> GetSeriesSize(int seriesId)
    {
        return Ok(await _unitOfWork.SeriesRepository.GetFilesizeForSeriesAsync(seriesId));
    }

    /// <summary>
    /// For a set of series, return the size in bytes
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    [HttpPost("bulk-series-size")]
    public async Task<ActionResult<Dictionary<int, long>>> GetBulkSeriesSize([FromBody] IList<int> seriesIds)
    {
        return Ok(await _unitOfWork.SeriesRepository.GetFilesizeForMultipleSeriesAsync(seriesIds));
    }


    /// <summary>
    /// Downloads all chapters within a volume. If the chapters are multiple zips, they will all be zipped up.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [Authorize(PolicyGroups.DownloadPolicy)]
    [HttpGet("volume")]
    public async Task<ActionResult> DownloadVolume(int volumeId, [FromQuery] string? correlationId = null)
    {
        if (!await HasDownloadPermission()) return BadRequest(await _localizationService.Translate(UserId, "permission-denied"));
        var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId);
        if (volume == null) return BadRequest(await _localizationService.Translate(UserId, "volume-doesnt-exist"));
        var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);
        try
        {
            return await DownloadFiles(files, $"download_{Username!}_v{volumeId}", $"{series!.Name} - Volume {volume.Name}.zip", correlationId);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<bool> HasDownloadPermission()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        if (user == null) return false;
        return await _accountService.HasDownloadPermission(user);
    }

    private PhysicalFileResult GetFirstFileDownload(IEnumerable<MangaFile> files)
    {
        var (zipFile, contentType, fileDownloadName) = _downloadService.GetFirstFileDownload(files);
        return PhysicalFile(zipFile, contentType, fileDownloadName, true);
    }

    /// <summary>
    /// Returns the zip for a single chapter. If the chapter contains multiple files, they will be zipped.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter")]
    public async Task<ActionResult> DownloadChapter(int chapterId, [FromQuery] string? correlationId = null)
    {
        if (!await HasDownloadPermission()) return BadRequest(await _localizationService.Translate(UserId, "permission-denied"));
        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(UserId, "chapter-doesnt-exist"));
        var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume!.SeriesId);
        try
        {
            return await DownloadFiles(files, $"download_{Username!}_c{chapterId}", $"{series!.Name} - Chapter {chapter.GetNumberTitle()}.zip", correlationId);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<ActionResult> DownloadFiles(ICollection<MangaFile> files, string tempFolder, string downloadName, string? correlationId = null)
    {
        var username = Username!;
        var filename = Path.GetFileNameWithoutExtension(downloadName);
        try
        {
            await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                MessageFactory.DownloadProgressEvent(username,
                    filename, $"Downloading {filename}", 0F, "started", correlationId));

            if (files.Count == 1 && files.First().Format != MangaFormat.Image)
            {
                // Emit "ended" after the response is fully sent to the client
                HttpContext.Response.OnCompleted(async () =>
                {
                    await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                        MessageFactory.DownloadProgressEvent(username,
                            filename, "Download Complete", 1F, "ended", correlationId));
                });
                return GetFirstFileDownload(files);
            }

            var filePath = _archiveService.CreateZipFromFoldersForDownload(files.Select(c => c.FilePath).ToList(), tempFolder, ProgressCallback);

            await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                MessageFactory.DownloadProgressEvent(username,
                    filename, "Download Complete", 1F, "ended", correlationId));

            return PhysicalFile(filePath, DefaultContentType, downloadName, true);

            async Task ProgressCallback(Tuple<string, float> progressInfo)
            {
                await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                    MessageFactory.DownloadProgressEvent(username, filename, $"Processing {Path.GetFileNameWithoutExtension(progressInfo.Item1)}",
                        Math.Clamp(progressInfo.Item2, 0F, 1F), "updated", correlationId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when trying to download files");
            await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
                MessageFactory.DownloadProgressEvent(Username!,
                    filename, "Download Complete", 1F, "ended", correlationId));
            throw;
        }
    }

    [HttpGet("series")]
    public async Task<ActionResult> DownloadSeries(int seriesId, [FromQuery] string? correlationId = null)
    {
        if (!await HasDownloadPermission()) return BadRequest(await _localizationService.Translate(UserId, "permission-denied"));

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null) return BadRequest("Invalid Series");

        var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
        try
        {
            return await DownloadFiles(files, $"download_{Username!}_s{seriesId}", $"{series.Name}.zip", correlationId);
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
    public async Task<ActionResult> DownloadBookmarkPages(DownloadBookmarkDto downloadBookmarkDto)
    {
        if (!await HasDownloadPermission()) return BadRequest(await _localizationService.Translate(UserId, "permission-denied"));
        if (!downloadBookmarkDto.Bookmarks.Any()) return BadRequest(await _localizationService.Translate(UserId, "bookmarks-empty"));

        // We know that all bookmarks will be for one single seriesId
        var userId = UserId;
        var username = Username!;
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(downloadBookmarkDto.Bookmarks.First().SeriesId);

        var files = await _bookmarkService.GetBookmarkFilesById(downloadBookmarkDto.Bookmarks.Select(b => b.Id));

        var filename = $"{series!.Name} - Bookmarks.zip";
        await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
            MessageFactory.DownloadProgressEvent(username, Path.GetFileNameWithoutExtension(filename), $"Downloading {filename}",0F));
        var seriesIds = string.Join("_", downloadBookmarkDto.Bookmarks.Select(b => b.SeriesId).Distinct());
        var filePath =  _archiveService.CreateZipForDownload(files,
            $"download_{userId}_{seriesIds}_bookmarks");
        await _eventHub.SendMessageAsync(MessageFactory.DownloadProgress,
            MessageFactory.DownloadProgressEvent(username, Path.GetFileNameWithoutExtension(filename), $"Downloading {filename}", 1F));


        return PhysicalFile(filePath, DefaultContentType, Uri.EscapeDataString(filename), true);
    }

}
