﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.DTOs.Downloads;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace API.Controllers
{
    [Authorize(Policy = "RequireDownloadRole")]
    public class DownloadController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly IDownloadService _downloadService;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly NumericComparer _numericComparer;
        private const string DefaultContentType = "application/octet-stream";

        public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService,
            ICacheService cacheService, IDownloadService downloadService, IHubContext<MessageHub> messageHub)
        {
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _cacheService = cacheService;
            _downloadService = downloadService;
            _messageHub = messageHub;
            _numericComparer = new NumericComparer();
        }

        [HttpGet("volume-size")]
        public async Task<ActionResult<long>> GetVolumeSize(int volumeId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
            return Ok(_directoryService.GetTotalSize(files.Select(c => c.FilePath)));
        }

        [HttpGet("chapter-size")]
        public async Task<ActionResult<long>> GetChapterSize(int chapterId)
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
            return Ok(_directoryService.GetTotalSize(files.Select(c => c.FilePath)));
        }

        [HttpGet("series-size")]
        public async Task<ActionResult<long>> GetSeriesSize(int seriesId)
        {
            var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
            return Ok(_directoryService.GetTotalSize(files.Select(c => c.FilePath)));
        }

        [HttpGet("volume")]
        public async Task<ActionResult> DownloadVolume(int volumeId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId);
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);
            try
            {
                return await DownloadFiles(files, $"download_{User.GetUsername()}_v{volumeId}", $"{series.Name} - Volume {volume.Number}.zip");
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<ActionResult> GetFirstFileDownload(IEnumerable<MangaFile> files)
        {
            var (bytes, contentType, fileDownloadName) = await _downloadService.GetFirstFileDownload(files);
            return File(bytes, contentType, fileDownloadName);
        }

        [HttpGet("chapter")]
        public async Task<ActionResult> DownloadChapter(int chapterId)
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId);
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);
            try
            {
                return await DownloadFiles(files, $"download_{User.GetUsername()}_c{chapterId}", $"{series.Name} - Chapter {chapter.Number}.zip");
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<ActionResult> DownloadFiles(ICollection<MangaFile> files, string tempFolder, string downloadName)
        {
            await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(), Path.GetFileNameWithoutExtension(downloadName), 0F));
            if (files.Count == 1)
            {
                return await GetFirstFileDownload(files);
            }
            var (fileBytes, _) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                tempFolder);
            await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(), Path.GetFileNameWithoutExtension(downloadName), 1F));
            return File(fileBytes, DefaultContentType, downloadName);
        }

        [HttpGet("series")]
        public async Task<ActionResult> DownloadSeries(int seriesId)
        {
            var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            try
            {
                return await DownloadFiles(files, $"download_{User.GetUsername()}_s{seriesId}", $"{series.Name}.zip");
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("bookmarks")]
        public async Task<ActionResult> DownloadBookmarkPages(DownloadBookmarkDto downloadBookmarkDto)
        {
            // We know that all bookmarks will be for one single seriesId
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(downloadBookmarkDto.Bookmarks.First().SeriesId);

            var bookmarkDirectory =
                (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
            var files = (await _unitOfWork.UserRepository.GetAllBookmarksByIds(downloadBookmarkDto.Bookmarks
                .Select(b => b.Id)
                .ToList()))
                .Select(b => Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(bookmarkDirectory, b.FileName)));

            var (fileBytes, _) = await _archiveService.CreateZipForDownload(files,
                $"download_{user.Id}_{series.Id}_bookmarks");
            return File(fileBytes, DefaultContentType, $"{series.Name} - Bookmarks.zip");
        }

    }
}
