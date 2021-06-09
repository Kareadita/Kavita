using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize(Policy = "RequireDownloadRole")]
    public class DownloadController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;

        public DownloadController(IDirectoryService directoryService, IUnitOfWork unitOfWork, IArchiveService archiveService)
        {
            _directoryService = directoryService;
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
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
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath), 
                    $"download_{User.GetUsername()}_v{volumeId}");
                return File(fileBytes, "application/zip", Path.GetFileName(zipPath));  
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("chapter")]
        public async Task<ActionResult> DownloadChapter(int chapterId)
        {
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapter(chapterId);
            try
            {
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath), 
                    $"download_{User.GetUsername()}_c{chapterId}");
                return File(fileBytes, "application/zip", Path.GetFileName(zipPath));  
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
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath), 
                    $"download_{User.GetUsername()}_s{seriesId}");
                return File(fileBytes, "application/zip", Path.GetFileName(zipPath));  
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}