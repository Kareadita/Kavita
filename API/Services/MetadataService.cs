using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Extensions;
using API.Helpers;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface IMetadataService
{
    /// <summary>
    /// Recalculates cover images for all entities in a library.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate"></param>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task GenerateCoversForLibrary(int libraryId, bool forceUpdate = false, bool forceColorScape = false);
    /// <summary>
    /// Performs a forced refresh of cover images just for a series, and it's nested entities
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="forceUpdate">Overrides any cache logic and forces execution</param>

    Task GenerateCoversForSeries(int libraryId, int seriesId, bool forceUpdate = true, bool forceColorScape = true);
    Task GenerateCoversForSeries(Series series, EncodeFormat encodeFormat, CoverImageSize coverImageSize, bool forceUpdate = false, bool forceColorScape = true);
    Task RemoveAbandonedMetadataKeys();
}

/// <summary>
/// Handles everything around Cover/ColorScape management
/// </summary>
public class MetadataService : IMetadataService
{
    public const string Name = "MetadataService";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MetadataService> _logger;
    private readonly IEventHub _eventHub;
    private readonly ICacheHelper _cacheHelper;
    private readonly IReadingItemService _readingItemService;
    private readonly IDirectoryService _directoryService;
    private readonly IImageService _imageService;
    private readonly IList<SignalRMessage> _updateEvents = new List<SignalRMessage>();

    public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger,
        IEventHub eventHub, ICacheHelper cacheHelper,
        IReadingItemService readingItemService, IDirectoryService directoryService,
        IImageService imageService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
        _cacheHelper = cacheHelper;
        _readingItemService = readingItemService;
        _directoryService = directoryService;
        _imageService = imageService;
    }

    /// <summary>
    /// Updates the metadata for a Chapter
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    /// <param name="encodeFormat">Convert image to Encoding Format when extracting the cover</param>
    /// <param name="forceColorScape">Force colorscape gen</param>
    private bool UpdateChapterCoverImage(Chapter? chapter, bool forceUpdate, EncodeFormat encodeFormat, CoverImageSize coverImageSize, bool forceColorScape = false)
    {
        if (chapter == null) return false;

        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null) return false;

        if (!_cacheHelper.ShouldUpdateCoverImage(
                _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, chapter.CoverImage),
                firstFile, chapter.Created, forceUpdate, chapter.CoverImageLocked))
        {
            if (NeedsColorSpace(chapter, forceColorScape))
            {
                _imageService.UpdateColorScape(chapter);
                _unitOfWork.ChapterRepository.Update(chapter);
                _updateEvents.Add(MessageFactory.CoverUpdateEvent(chapter.Id, MessageFactoryEntityTypes.Chapter));
            }

            return false;
        }


        _logger.LogDebug("[MetadataService] Generating cover image for {File}", firstFile.FilePath);

        chapter.CoverImage = _readingItemService.GetCoverImage(firstFile.FilePath,
            ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId), firstFile.Format, encodeFormat, coverImageSize);

        _imageService.UpdateColorScape(chapter);

        _unitOfWork.ChapterRepository.Update(chapter);

        _updateEvents.Add(MessageFactory.CoverUpdateEvent(chapter.Id, MessageFactoryEntityTypes.Chapter));
        return true;
    }

    private void UpdateChapterLastModified(Chapter chapter, bool forceUpdate)
    {
        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null || _cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) return;

        firstFile.UpdateLastModified();
    }

    private static bool NeedsColorSpace(IHasCoverImage? entity, bool force)
    {
        if (entity == null) return false;
        if (force) return true;

        return !string.IsNullOrEmpty(entity.CoverImage) &&
               (string.IsNullOrEmpty(entity.PrimaryColor) || string.IsNullOrEmpty(entity.SecondaryColor));
    }



    /// <summary>
    /// Updates the cover image for a Volume
    /// </summary>
    /// <param name="volume"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    /// <param name="forceColorScape">Force updating colorscape</param>
    private bool UpdateVolumeCoverImage(Volume? volume, bool forceUpdate, bool forceColorScape = false)
    {
        // We need to check if Volume coverImage matches first chapters if forceUpdate is false
        if (volume == null) return false;

        if (!_cacheHelper.ShouldUpdateCoverImage(
                _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, volume.CoverImage),
                null, volume.Created, forceUpdate))
        {
            if (NeedsColorSpace(volume, forceColorScape))
            {
                _imageService.UpdateColorScape(volume);
                _unitOfWork.VolumeRepository.Update(volume);
                _updateEvents.Add(MessageFactory.CoverUpdateEvent(volume.Id, MessageFactoryEntityTypes.Volume));
            }
            return false;
        }

        if (!volume.CoverImageLocked)
        {
            // For cover selection, chapters need to try for issue 1 first, then fallback to first sort order
            volume.Chapters ??= new List<Chapter>();

            var firstChapter = volume.Chapters.FirstOrDefault(x => x.MinNumber.Is(1f));
            if (firstChapter == null)
            {
                firstChapter = volume.Chapters.MinBy(x => x.SortOrder, ChapterSortComparerDefaultFirst.Default);
                if (firstChapter == null) return false;
            }

            volume.CoverImage = firstChapter.CoverImage;
        }
        _imageService.UpdateColorScape(volume);

        _updateEvents.Add(MessageFactory.CoverUpdateEvent(volume.Id, MessageFactoryEntityTypes.Volume));

        return true;
    }

    /// <summary>
    /// Updates cover image for Series
    /// </summary>
    /// <param name="series"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    private void UpdateSeriesCoverImage(Series? series, bool forceUpdate, bool forceColorScape = false)
    {
        if (series == null) return;

        if (!_cacheHelper.ShouldUpdateCoverImage(
                _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, series.CoverImage),
                null, series.Created, forceUpdate, series.CoverImageLocked))
        {
            // Check if we don't have a primary/seconary color
            if (NeedsColorSpace(series, forceColorScape))
            {
                _imageService.UpdateColorScape(series);
                _updateEvents.Add(MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series));
            }

            return;
        }

        series.Volumes ??= [];
        series.CoverImage = series.GetCoverImage();

        _imageService.UpdateColorScape(series);

        _updateEvents.Add(MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series));
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="series"></param>
    /// <param name="forceUpdate"></param>
    /// <param name="encodeFormat"></param>
    private void ProcessSeriesCoverGen(Series series, bool forceUpdate, EncodeFormat encodeFormat, CoverImageSize coverImageSize, bool forceColorScape = false)
    {
        _logger.LogDebug("[MetadataService] Processing cover image generation for series: {SeriesName}", series.OriginalName);
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
                    var chapterUpdated = UpdateChapterCoverImage(chapter, forceUpdate, encodeFormat, coverImageSize, forceColorScape);
                    // If cover was update, either the file has changed or first scan, and we should force a metadata update
                    UpdateChapterLastModified(chapter, forceUpdate || chapterUpdated);
                    if (index == 0 && chapterUpdated)
                    {
                        firstChapterUpdated = true;
                    }

                    index++;
                }

                var volumeUpdated = UpdateVolumeCoverImage(volume, firstChapterUpdated || forceUpdate, forceColorScape);
                if (volumeIndex == 0 && volumeUpdated)
                {
                    firstVolumeUpdated = true;
                }
                volumeIndex++;
            }

            UpdateSeriesCoverImage(series, firstVolumeUpdated || forceUpdate, forceColorScape);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MetadataService] There was an exception during cover generation for {SeriesName} ", series.Name);
        }
    }


    /// <summary>
    /// Refreshes Cover Images for a whole library
    /// </summary>
    /// <remarks>This can be heavy on memory first run</remarks>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    /// <param name="forceColorScape">Force updating colorscape</param>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task GenerateCoversForLibrary(int libraryId, bool forceUpdate = false, bool forceColorScape = false)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        if (library == null) return;
        _logger.LogInformation("[MetadataService] Beginning cover generation refresh of {LibraryName}", library.Name);

        _updateEvents.Clear();

        var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
        var stopwatch = Stopwatch.StartNew();
        var totalTime = 0L;
        _logger.LogInformation("[MetadataService] Refreshing Library {LibraryName} for cover generation. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CoverUpdateProgressEvent(library.Id, 0F, ProgressEventType.Started, $"Starting {library.Name}"));

        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var encodeFormat = settings.EncodeMediaAs;
        var coverImageSize = settings.CoverImageSize;

        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
        {
            if (chunkInfo.TotalChunks == 0) continue;
            totalTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            _logger.LogDebug("[MetadataService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd})",
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
                    MessageFactory.CoverUpdateProgressEvent(library.Id, progress, ProgressEventType.Updated, series.Name));

                try
                {
                    ProcessSeriesCoverGen(series, forceUpdate, encodeFormat, coverImageSize, forceColorScape);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MetadataService] There was an exception during cover generation refresh for {SeriesName}", series.Name);
                }
                seriesIndex++;
            }

            await _unitOfWork.CommitAsync();

            await FlushEvents();

            _logger.LogInformation(
                "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CoverUpdateProgressEvent(library.Id, 1F, ProgressEventType.Ended, $"Complete"));

        _logger.LogInformation("[MetadataService] Updated covers for {SeriesNumber} series in library {LibraryName} in {ElapsedMilliseconds} milliseconds total", chunkInfo.TotalSize, library.Name, totalTime);
    }


    public async Task RemoveAbandonedMetadataKeys()
    {
        await _unitOfWork.TagRepository.RemoveAllTagNoLongerAssociated();
        await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
        await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
        await _unitOfWork.CollectionTagRepository.RemoveCollectionsWithoutSeries();
        await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();

    }

    /// <summary>
    /// Refreshes Metadata for a Series. Will always force updates.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="forceUpdate">Overrides any cache logic and forces execution</param>
    /// <param name="forceColorScape">Will ensure that the colorscape is regenerated</param>
    public async Task GenerateCoversForSeries(int libraryId, int seriesId, bool forceUpdate = true, bool forceColorScape = true)
    {
        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        if (series == null)
        {
            _logger.LogError("[MetadataService] Series {SeriesId} was not found on Library {LibraryId}", seriesId, libraryId);
            return;
        }

        // TODO: Cache this because it's called a lot during scans
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var encodeFormat = settings.EncodeMediaAs;
        var coverImageSize = settings.CoverImageSize;

        await GenerateCoversForSeries(series, encodeFormat, coverImageSize, forceUpdate, forceColorScape);
    }

    /// <summary>
    /// Generate Cover for a Series. This is used by Scan Loop and should not be invoked directly via User Interaction.
    /// </summary>
    /// <param name="series">A full Series, with metadata, chapters, etc</param>
    /// <param name="encodeFormat">When saving the file, what encoding should be used</param>
    /// <param name="forceUpdate"></param>
    /// <param name="forceColorScape">Forces just colorscape generation</param>
    public async Task GenerateCoversForSeries(Series series, EncodeFormat encodeFormat, CoverImageSize coverImageSize, bool forceUpdate = false, bool forceColorScape = true)
    {
        var sw = Stopwatch.StartNew();
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CoverUpdateProgressEvent(series.LibraryId, 0F, ProgressEventType.Started, series.Name));

        ProcessSeriesCoverGen(series, forceUpdate, encodeFormat, coverImageSize, forceColorScape);


        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("[MetadataService] Updated covers for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CoverUpdateProgressEvent(series.LibraryId, 1F, ProgressEventType.Ended, series.Name));

        await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate, MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series), false);
        await FlushEvents();
    }

    private async Task FlushEvents()
    {
        // Send all events out now that entities are saved
        _logger.LogDebug("Dispatching {Count} update events", _updateEvents.Count);
        foreach (var updateEvent in _updateEvents)
        {
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate, updateEvent, false);
        }
        _updateEvents.Clear();
    }
}
