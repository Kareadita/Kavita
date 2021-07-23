using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace API.Controllers
{
    [Authorize(Policy = "RequireDownloadRole")]
    public class DownloadController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;

        public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService)
        {
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
        }

        [HttpGet("volume-size")]
        public async Task<ActionResult<long>> GetVolumeSize(int volumeId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
            return Ok(DirectoryService.GetTotalSize(files.Select(c => c.FilePath)));
        }

        [HttpGet("chapter-size")]
        public async Task<ActionResult<long>> GetChapterSize(int chapterId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapter(chapterId);
            return Ok(DirectoryService.GetTotalSize(files.Select(c => c.FilePath)));
        }

        [HttpGet("series-size")]
        public async Task<ActionResult<long>> GetSeriesSize(int seriesId)
        {
            var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
            return Ok(DirectoryService.GetTotalSize(files.Select(c => c.FilePath)));
        }

        [HttpGet("volume")]
        public async Task<ActionResult> DownloadVolume(int volumeId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForVolume(volumeId);
            try
            {
                if (files.Count == 1)
                {
                    return await GetFirstFileDownload(files);
                }
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    $"download_{User.GetUsername()}_v{volumeId}");
                return File(fileBytes, "application/zip", Path.GetFileNameWithoutExtension(zipPath) + ".zip");
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<ActionResult> GetFirstFileDownload(IEnumerable<MangaFile> files)
        {
            var firstFile = files.Select(c => c.FilePath).First();
            var fileProvider = new FileExtensionContentTypeProvider();
            // Figures out what the content type should be based on the file name.
            if (!fileProvider.TryGetContentType(firstFile, out var contentType))
            {
                contentType = Path.GetExtension(firstFile).ToLowerInvariant() switch
                {
                    ".cbz" => "application/zip",
                    ".cbr" => "application/vnd.rar",
                    ".cb7" => "application/x-compressed",
                    ".epub" => "application/epub+zip",
                    ".7z" => "application/x-7z-compressed",
                    ".7zip" => "application/x-7z-compressed",
                    ".pdf" => "application/pdf",
                    _ => contentType
                };
            }

            return File(await _directoryService.ReadFileAsync(firstFile), contentType, Path.GetFileName(firstFile));
        }

        [HttpGet("chapter")]
        public async Task<ActionResult> DownloadChapter(int chapterId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapter(chapterId);
            try
            {
                if (files.Count == 1)
                {
                    return await GetFirstFileDownload(files);
                }
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    $"download_{User.GetUsername()}_c{chapterId}");
                return File(fileBytes, "application/zip", Path.GetFileNameWithoutExtension(zipPath) + ".zip");
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("series")]
        public async Task<ActionResult> DownloadSeries(int seriesId)
        {
            var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
            try
            {
                if (files.Count == 1)
                {
                    return await GetFirstFileDownload(files);
                }
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    $"download_{User.GetUsername()}_s{seriesId}");
                return File(fileBytes, "application/zip", Path.GetFileNameWithoutExtension(zipPath) + ".zip");
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
