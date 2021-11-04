using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.DTOs.Downloads;
using API.Entities;
using API.Entities.Enums;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly IDownloadService _downloadService;
        private readonly NumericComparer _numericComparer;
        private const string DefaultContentType = "application/octet-stream";

        public DownloadController(IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService, ICacheService cacheService, IDownloadService downloadService)
        {
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _cacheService = cacheService;
            _downloadService = downloadService;
            _numericComparer = new NumericComparer();
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
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
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
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId);
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);
            try
            {
                if (files.Count == 1)
                {
                    return await GetFirstFileDownload(files);
                }
                var (fileBytes, _) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    $"download_{User.GetUsername()}_v{volumeId}");
                return File(fileBytes, DefaultContentType, $"{series.Name} - Volume {volume.Number}.zip");
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
                if (files.Count == 1)
                {
                    return await GetFirstFileDownload(files);
                }
                var (fileBytes, _) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    $"download_{User.GetUsername()}_c{chapterId}");
                return File(fileBytes, DefaultContentType, $"{series.Name} - Chapter {chapter.Number}.zip");
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
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            try
            {
                if (files.Count == 1)
                {
                    return await GetFirstFileDownload(files);
                }
                var (fileBytes, _) = await _archiveService.CreateZipForDownload(files.Select(c => c.FilePath),
                    $"download_{User.GetUsername()}_s{seriesId}");
                return File(fileBytes, DefaultContentType, $"{series.Name}.zip");
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
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(downloadBookmarkDto.Bookmarks.First().SeriesId);
            var totalFilePaths = new List<string>();

            var tempFolder = $"download_{series.Id}_bookmarks";
            var fullExtractPath = Path.Join(DirectoryService.TempDirectory, tempFolder);
            if (new DirectoryInfo(fullExtractPath).Exists)
            {
                return BadRequest(
                    "Server is currently processing this exact download. Please try again in a few minutes.");
            }
            DirectoryService.ExistOrCreate(fullExtractPath);

            var uniqueChapterIds = downloadBookmarkDto.Bookmarks.Select(b => b.ChapterId).Distinct().ToList();

            foreach (var chapterId in uniqueChapterIds)
            {
                var chapterExtractPath = Path.Join(fullExtractPath, $"{series.Id}_bookmark_{chapterId}");
                var chapterPages = downloadBookmarkDto.Bookmarks.Where(b => b.ChapterId == chapterId)
                    .Select(b => b.Page).ToList();
                var mangaFiles = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
                switch (series.Format)
                {
                    case MangaFormat.Image:
                        DirectoryService.ExistOrCreate(chapterExtractPath);
                        _directoryService.CopyFilesToDirectory(mangaFiles.Select(f => f.FilePath), chapterExtractPath, $"{chapterId}_");
                        break;
                    case MangaFormat.Archive:
                    case MangaFormat.Pdf:
                        _cacheService.ExtractChapterFiles(chapterExtractPath, mangaFiles.ToList());
                        var originalFiles = DirectoryService.GetFilesWithExtension(chapterExtractPath,
                            Parser.Parser.ImageFileExtensions);
                        _directoryService.CopyFilesToDirectory(originalFiles, chapterExtractPath, $"{chapterId}_");
                        DirectoryService.DeleteFiles(originalFiles);
                        break;
                    case MangaFormat.Epub:
                        return BadRequest("Series is not in a valid format.");
                    default:
                        return BadRequest("Series is not in a valid format. Please rescan series and try again.");
                }

                var files = DirectoryService.GetFilesWithExtension(chapterExtractPath, Parser.Parser.ImageFileExtensions);
                // Filter out images that aren't in bookmarks
                Array.Sort(files, _numericComparer);
                totalFilePaths.AddRange(files.Where((_, i) => chapterPages.Contains(i)));
            }


            var (fileBytes, _) = await _archiveService.CreateZipForDownload(totalFilePaths,
                tempFolder);
            DirectoryService.ClearAndDeleteDirectory(fullExtractPath);
            return File(fileBytes, DefaultContentType, $"{series.Name} - Bookmarks.zip");
        }
    }
}
