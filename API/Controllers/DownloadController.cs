﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Downloads;
using API.Entities;
using API.Extensions;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

/// <summary>
/// All APIs related to downloading entities from the system. Requires Download Role or Admin Role.
/// </summary>
[Authorize(Policy="RequireDownloadRole")]
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
    private const string DefaultContentType = "application/octet-stream";

    public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService,
        IDownloadService downloadService, IEventHub eventHub, ILogger<DownloadController> logger, IBookmarkService bookmarkService,
        IAccountService accountService)
    {
        _unitOfWork = unitOfWork;
        _archiveService = archiveService;
        _directoryService = directoryService;
        _downloadService = downloadService;
        _eventHub = eventHub;
        _logger = logger;
        _bookmarkService = bookmarkService;
        _accountService = accountService;
    }

    /// <summary>
    /// For a given volume, return the size in bytes
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("volume-size")]
    public async Task<ActionResult<long>> GetVolumeSize(int volumeId)
    {
        var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
        return Ok(_directoryService.GetTotalSize(files.Select(c => c.FilePath)));
    }

    /// <summary>
    /// For a given chapter, return the size in bytes
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-size")]
    public async Task<ActionResult<long>> GetChapterSize(int chapterId)
    {
        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
        return Ok(_directoryService.GetTotalSize(files.Select(c => c.FilePath)));
    }

    /// <summary>
    /// For a series, return the size in bytes
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("series-size")]
    public async Task<ActionResult<long>> GetSeriesSize(int seriesId)
    {
        var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
        return Ok(_directoryService.GetTotalSize(files.Select(c => c.FilePath)));
    }


    /// <summary>
    /// Downloads all chapters within a volume. If the chapters are multiple zips, they will all be zipped up.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [Authorize(Policy="RequireDownloadRole")]
    [HttpGet("volume")]
    public async Task<ActionResult> DownloadVolume(int volumeId)
    {
        if (!await HasDownloadPermission()) return BadRequest("You do not have permission");
        var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId);
        if (volume == null) return BadRequest("Volume doesn't exist");
        var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);
        try
        {
            return await DownloadFiles(files, $"download_{User.GetUsername()}_v{volumeId}", $"{series!.Name} - Volume {volume.Number}.zip");
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<bool> HasDownloadPermission()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return false;
        return await _accountService.HasDownloadPermission(user);
    }

    private ActionResult GetFirstFileDownload(IEnumerable<MangaFile> files)
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
    public async Task<ActionResult> DownloadChapter(int chapterId)
    {
        if (!await HasDownloadPermission()) return BadRequest("You do not have permission");
        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest("Invalid chapter");
        var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume!.SeriesId);
        try
        {
            return await DownloadFiles(files, $"download_{User.GetUsername()}_c{chapterId}", $"{series!.Name} - Chapter {chapter.Number}.zip");
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<ActionResult> DownloadFiles(ICollection<MangaFile> files, string tempFolder, string downloadName)
    {
        try
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(),
                    Path.GetFileNameWithoutExtension(downloadName), 0F, "started"));
            if (files.Count == 1)
            {
                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.DownloadProgressEvent(User.GetUsername(),
                        Path.GetFileNameWithoutExtension(downloadName), 1F, "ended"));
                return GetFirstFileDownload(files);
            }

            var filePath = _archiveService.CreateZipForDownload(files.Select(c => c.FilePath), tempFolder);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(),
                    Path.GetFileNameWithoutExtension(downloadName), 1F, "ended"));
            return PhysicalFile(filePath, DefaultContentType, downloadName, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when trying to download files");
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(),
                    Path.GetFileNameWithoutExtension(downloadName), 1F, "ended"));
            throw;
        }
    }

    [HttpGet("series")]
    public async Task<ActionResult> DownloadSeries(int seriesId)
    {
        if (!await HasDownloadPermission()) return BadRequest("You do not have permission");
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null) return BadRequest("Invalid Series");
        var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
        try
        {
            return await DownloadFiles(files, $"download_{User.GetUsername()}_s{seriesId}", $"{series.Name}.zip");
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
        if (!await HasDownloadPermission()) return BadRequest("You do not have permission");
        if (!downloadBookmarkDto.Bookmarks.Any()) return BadRequest("Bookmarks cannot be empty");

        // We know that all bookmarks will be for one single seriesId
        var userId = User.GetUserId()!;
        var username = User.GetUsername()!;
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(downloadBookmarkDto.Bookmarks.First().SeriesId);

        var files = await _bookmarkService.GetBookmarkFilesById(downloadBookmarkDto.Bookmarks.Select(b => b.Id));

        var filename = $"{series!.Name} - Bookmarks.zip";
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.DownloadProgressEvent(username, Path.GetFileNameWithoutExtension(filename), 0F));
        var seriesIds = string.Join("_", downloadBookmarkDto.Bookmarks.Select(b => b.SeriesId).Distinct());
        var filePath =  _archiveService.CreateZipForDownload(files,
            $"download_{userId}_{seriesIds}_bookmarks");
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.DownloadProgressEvent(username, Path.GetFileNameWithoutExtension(filename), 1F));


        return PhysicalFile(filePath, DefaultContentType, filename, true);
    }

}
