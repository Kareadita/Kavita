using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Downloads;
using API.Entities;
using API.Extensions;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
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
        private const string DefaultContentType = "application/octet-stream";

        public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService,
            IDownloadService downloadService, IEventHub eventHub, ILogger<DownloadController> logger, IBookmarkService bookmarkService)
        {
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _downloadService = downloadService;
            _eventHub = eventHub;
            _logger = logger;
            _bookmarkService = bookmarkService;
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
        /// Downloads all chapters within a volume.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        [Authorize(Policy="RequireDownloadRole")]
        [HttpGet("volume")]
        public async Task<ActionResult> DownloadVolume(int volumeId)
        {
            if (!await HasDownloadPermission()) return BadRequest("You do not have permission");

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

        private async Task<bool> HasDownloadPermission()
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            return await _downloadService.HasDownloadPermission(user);
        }

        private async Task<ActionResult> GetFirstFileDownload(IEnumerable<MangaFile> files)
        {
            var (bytes, contentType, fileDownloadName) = await _downloadService.GetFirstFileDownload(files);
            return File(bytes, contentType, fileDownloadName);
        }

        [HttpGet("chapter")]
        public async Task<ActionResult> DownloadChapter(int chapterId)
        {
            if (!await HasDownloadPermission()) return BadRequest("You do not have permission");
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
                    return await GetFirstFileDownload(files);
                }

                var (fileBytes, _) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    tempFolder);
                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.DownloadProgressEvent(User.GetUsername(),
                        Path.GetFileNameWithoutExtension(downloadName), 1F, "ended"));
                return File(fileBytes, DefaultContentType, downloadName);
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
            if (!await HasDownloadPermission()) return BadRequest("You do not have permission");

            // We know that all bookmarks will be for one single seriesId
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(downloadBookmarkDto.Bookmarks.First().SeriesId);

            var files = await _bookmarkService.GetBookmarkFilesById(downloadBookmarkDto.Bookmarks.Select(b => b.Id));

            var filename = $"{series.Name} - Bookmarks.zip";
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(), Path.GetFileNameWithoutExtension(filename), 0F));
            var (fileBytes, _) = await _archiveService.CreateZipForDownload(files,
                $"download_{user.Id}_{series.Id}_bookmarks");
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(), Path.GetFileNameWithoutExtension(filename), 1F));


            return File(fileBytes, DefaultContentType, filename);
        }

    }
}
