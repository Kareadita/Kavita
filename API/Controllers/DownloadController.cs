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
using API.Extensions;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize(Policy="RequireDownloadRole")]
    public class DownloadController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;
        private readonly IDownloadService _downloadService;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<DownloadController> _logger;
        private const string DefaultContentType = "application/octet-stream";

        public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService,
            IDownloadService downloadService, IHubContext<MessageHub> messageHub, UserManager<AppUser> userManager, ILogger<DownloadController> logger)
        {
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _downloadService = downloadService;
            _messageHub = messageHub;
            _userManager = userManager;
            _logger = logger;
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
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains(PolicyConstants.DownloadRole) || roles.Contains(PolicyConstants.AdminRole);
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
                await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                    MessageFactory.DownloadProgressEvent(User.GetUsername(),
                        Path.GetFileNameWithoutExtension(downloadName), 0F));
                if (files.Count == 1)
                {
                    await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                        MessageFactory.DownloadProgressEvent(User.GetUsername(),
                            Path.GetFileNameWithoutExtension(downloadName), 1F));
                    return await GetFirstFileDownload(files);
                }

                var (fileBytes, _) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    tempFolder);
                await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                    MessageFactory.DownloadProgressEvent(User.GetUsername(),
                        Path.GetFileNameWithoutExtension(downloadName), 1F));
                return File(fileBytes, DefaultContentType, downloadName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when trying to download files");
                await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                    MessageFactory.DownloadProgressEvent(User.GetUsername(),
                        Path.GetFileNameWithoutExtension(downloadName), 1F));
                throw ex;
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

            var bookmarkDirectory =
                (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

            var files = (await _unitOfWork.UserRepository.GetAllBookmarksByIds(downloadBookmarkDto.Bookmarks
                .Select(b => b.Id)
                .ToList()))
                .Select(b => Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(bookmarkDirectory, $"{b.ChapterId}_{b.FileName}")));

            var filename = $"{series.Name} - Bookmarks.zip";
            await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(), Path.GetFileNameWithoutExtension(filename), 0F));
            var (fileBytes, _) = await _archiveService.CreateZipForDownload(files,
                $"download_{user.Id}_{series.Id}_bookmarks");
            await _messageHub.Clients.All.SendAsync(SignalREvents.DownloadProgress,
                MessageFactory.DownloadProgressEvent(User.GetUsername(), Path.GetFileNameWithoutExtension(filename), 1F));
            return File(fileBytes, DefaultContentType, filename);
        }

    }
}
