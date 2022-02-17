using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Data.Scanner;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IMetadataService
{
    /// <summary>
    /// Recalculates metadata for all entities in a library.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate"></param>
    Task RefreshMetadata(int libraryId, bool forceUpdate = false);
    /// <summary>
    /// Performs a forced refresh of metadata just for a series and it's nested entities
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = false);
}

public class MetadataService : IMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MetadataService> _logger;
    private readonly IEventHub _eventHub;
    private readonly ICacheHelper _cacheHelper;
    private readonly IReadingItemService _readingItemService;
    private readonly IDirectoryService _directoryService;
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();
    public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger,
        IEventHub eventHub, ICacheHelper cacheHelper,
        IReadingItemService readingItemService, IDirectoryService directoryService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
        _cacheHelper = cacheHelper;
        _readingItemService = readingItemService;
        _directoryService = directoryService;
    }

    /// <summary>
    /// Updates the metadata for a Chapter
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    private async Task<bool> UpdateChapterCoverImage(Chapter chapter, bool forceUpdate)
    {
        var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();

        if (!_cacheHelper.ShouldUpdateCoverImage(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, chapter.CoverImage), firstFile, chapter.Created, forceUpdate, chapter.CoverImageLocked))
            return false;

        if (firstFile == null) return false;

        _logger.LogDebug("[MetadataService] Generating cover image for {File}", firstFile.FilePath);
        chapter.CoverImage = _readingItemService.GetCoverImage(firstFile.FilePath, ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId), firstFile.Format);
        await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
            MessageFactory.CoverUpdateEvent(chapter.Id, "chapter"), false);
        return true;
    }

    private void UpdateChapterLastModified(Chapter chapter, bool forceUpdate)
    {
        var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
        if (firstFile == null || _cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) return;

        firstFile.UpdateLastModified();
    }

    /// <summary>
    /// Updates the cover image for a Volume
    /// </summary>
    /// <param name="volume"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    private async Task<bool> UpdateVolumeCoverImage(Volume volume, bool forceUpdate)
    {
        // We need to check if Volume coverImage matches first chapters if forceUpdate is false
        if (volume == null || !_cacheHelper.ShouldUpdateCoverImage(
                _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, volume.CoverImage),
                null, volume.Created, forceUpdate)) return false;

        volume.Chapters ??= new List<Chapter>();
        var firstChapter = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).FirstOrDefault();
        if (firstChapter == null) return false;

        volume.CoverImage = firstChapter.CoverImage;
        await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate, MessageFactory.CoverUpdateEvent(volume.Id, "volume"), false);

        return true;
    }

    /// <summary>
    /// Updates cover image for Series
    /// </summary>
    /// <param name="series"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    private async Task UpdateSeriesCoverImage(Series series, bool forceUpdate)
    {
        if (series == null) return;

        if (!_cacheHelper.ShouldUpdateCoverImage(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, series.CoverImage),
                null, series.Created, forceUpdate, series.CoverImageLocked))
            return;

        series.Volumes ??= new List<Volume>();
        var firstCover = series.Volumes.GetCoverImage(series.Format);
        string coverImage = null;
        if (firstCover == null && series.Volumes.Any())
        {
            // If firstCover is null and one volume, the whole series is Chapters under Vol 0.
            if (series.Volumes.Count == 1)
            {
                coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparerForInChapterSorting)
                    .FirstOrDefault(c => !c.IsSpecial)?.CoverImage;
            }

            if (!_cacheHelper.CoverImageExists(coverImage))
            {
                coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparerForInChapterSorting)
                    .FirstOrDefault()?.CoverImage;
            }
        }
        series.CoverImage = firstCover?.CoverImage ?? coverImage;
        await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate, MessageFactory.CoverUpdateEvent(series.Id, "series"), false);
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="series"></param>
    /// <param name="forceUpdate"></param>
    private async Task ProcessSeriesMetadataUpdate(Series series, bool forceUpdate)
    {
        _logger.LogDebug("[MetadataService] Processing series {SeriesName}", series.OriginalName);
        try
        {
            var volumeIndex = 0;
            var firstVolumeUpdated = false;
            foreach (var volume in series.Volumes)
            {
                var firstChapterUpdated = false; // This only needs to be FirstChapter updated
                var index = 0;
                foreach (var chapter in volume.Chapters)
                {
                    var chapterUpdated = await UpdateChapterCoverImage(chapter, forceUpdate);
                    // If cover was update, either the file has changed or first scan and we should force a metadata update
                    UpdateChapterLastModified(chapter, forceUpdate || chapterUpdated);
                    if (index == 0 && chapterUpdated)
                    {
                        firstChapterUpdated = true;
                    }

                    index++;
                }

                var volumeUpdated = await UpdateVolumeCoverImage(volume, firstChapterUpdated || forceUpdate);
                if (volumeIndex == 0 && volumeUpdated)
                {
                    firstVolumeUpdated = true;
                }
                volumeIndex++;
            }

            await UpdateSeriesCoverImage(series, firstVolumeUpdated || forceUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MetadataService] There was an exception during updating metadata for {SeriesName} ", series.Name);
        }
    }


    /// <summary>
    /// Refreshes Metadata for a whole library
    /// </summary>
    /// <remarks>This can be heavy on memory first run</remarks>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    public async Task RefreshMetadata(int libraryId, bool forceUpdate = false)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.None);
        _logger.LogInformation("[MetadataService] Beginning metadata refresh of {LibraryName}", library.Name);

        var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
        var stopwatch = Stopwatch.StartNew();
        var totalTime = 0L;
        _logger.LogInformation("[MetadataService] Refreshing Library {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.RefreshMetadataProgressEvent(library.Id, 0F, $"Starting {library.Name}"));

        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
        {
            if (chunkInfo.TotalChunks == 0) continue;
            totalTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            _logger.LogInformation("[MetadataService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
                chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);

            var nonLibrarySeries = await _unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id,
                new UserParams()
                {
                    PageNumber = chunk,
                    PageSize = chunkInfo.ChunkSize
                });
            _logger.LogDebug("[MetadataService] Fetched {SeriesCount} series for refresh", nonLibrarySeries.Count);

            var seriesIndex = 0;
            foreach (var series in nonLibrarySeries)
            {
                var index = chunk * seriesIndex;
                var progress =  Math.Max(0F, Math.Min(1F, index * 1F / chunkInfo.TotalSize));

                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.RefreshMetadataProgressEvent(library.Id, progress, series.Name));

                try
                {
                    await ProcessSeriesMetadataUpdate(series, forceUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MetadataService] There was an exception during metadata refresh for {SeriesName}", series.Name);
                }
                seriesIndex++;
            }

            await _unitOfWork.CommitAsync();

            _logger.LogInformation(
                "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.RefreshMetadataProgressEvent(library.Id, 1F, $"Complete"));

        await RemoveAbandonedMetadataKeys();


        _logger.LogInformation("[MetadataService] Updated metadata for {SeriesNumber} series in library {LibraryName} in {ElapsedMilliseconds} milliseconds total", chunkInfo.TotalSize, library.Name, totalTime);
    }

    private async Task RemoveAbandonedMetadataKeys()
    {
        await _unitOfWork.TagRepository.RemoveAllTagNoLongerAssociated();
        await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
        await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
    }

    /// <summary>
    /// Refreshes Metadata for a Series. Will always force updates.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    public async Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = true)
    {
        var sw = Stopwatch.StartNew();
        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        if (series == null)
        {
            _logger.LogError("[MetadataService] Series {SeriesId} was not found on Library {LibraryId}", seriesId, libraryId);
            return;
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.RefreshMetadataProgressEvent(libraryId, 0F, series.Name));

        await ProcessSeriesMetadataUpdate(series, forceUpdate);


        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.RefreshMetadataProgressEvent(libraryId, 1F, series.Name));

        await RemoveAbandonedMetadataKeys();

        if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
        {
            // TODO: Fix CoverUpdate/RefreshMetadata from merge
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate, MessageFactory.CoverUpdateEvent(series.Id, "series"), false);
        }


        _logger.LogInformation("[MetadataService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
    }
}
