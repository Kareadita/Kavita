using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;
#nullable enable

public interface IProcessSeries
{
    /// <summary>
    /// Do not allow this Prime to be invoked by multiple threads. It will break the DB.
    /// </summary>
    /// <returns></returns>
    Task Prime();
    Task ProcessSeriesAsync(IList<ParserInfo> parsedInfos, Library library, bool forceUpdate = false);
    void EnqueuePostSeriesProcessTasks(int libraryId, int seriesId, bool forceUpdate = false);

    // These exists only for Unit testing
    void UpdateSeriesMetadata(Series series, Library library);
    void UpdateVolumes(Series series, IList<ParserInfo> parsedInfos, bool forceUpdate = false);
    void UpdateChapters(Series series, Volume volume, IList<ParserInfo> parsedInfos, bool forceUpdate = false);
    void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info, bool forceUpdate = false);
    void UpdateChapterFromComicInfo(Chapter chapter, ComicInfo? comicInfo, bool forceUpdate = false);
}

/// <summary>
/// All code needed to Update a Series from a Scan action
/// </summary>
public class ProcessSeries : IProcessSeries
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessSeries> _logger;
    private readonly IEventHub _eventHub;
    private readonly IDirectoryService _directoryService;
    private readonly ICacheHelper _cacheHelper;
    private readonly IReadingItemService _readingItemService;
    private readonly IFileService _fileService;
    private readonly IMetadataService _metadataService;
    private readonly IWordCountAnalyzerService _wordCountAnalyzerService;
    private readonly ICollectionTagService _collectionTagService;
    private readonly IReadingListService _readingListService;

    private Dictionary<string, Genre> _genres;
    private IList<Person> _people;
    private Dictionary<string, Tag> _tags;
    private Dictionary<string, CollectionTag> _collectionTags;

    public ProcessSeries(IUnitOfWork unitOfWork, ILogger<ProcessSeries> logger, IEventHub eventHub,
        IDirectoryService directoryService, ICacheHelper cacheHelper, IReadingItemService readingItemService,
        IFileService fileService, IMetadataService metadataService, IWordCountAnalyzerService wordCountAnalyzerService,
        ICollectionTagService collectionTagService, IReadingListService readingListService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
        _directoryService = directoryService;
        _cacheHelper = cacheHelper;
        _readingItemService = readingItemService;
        _fileService = fileService;
        _metadataService = metadataService;
        _wordCountAnalyzerService = wordCountAnalyzerService;
        _collectionTagService = collectionTagService;
        _readingListService = readingListService;


        _genres = new Dictionary<string, Genre>();
        _people = new List<Person>();
        _tags = new Dictionary<string, Tag>();
        _collectionTags = new Dictionary<string, CollectionTag>();
    }

    /// <summary>
    /// Invoke this before processing any series, just once to prime all the needed data during a scan
    /// </summary>
    public async Task Prime()
    {
        _genres = (await _unitOfWork.GenreRepository.GetAllGenresAsync()).ToDictionary(t => t.NormalizedTitle);
        _people = await _unitOfWork.PersonRepository.GetAllPeople();
        _tags = (await _unitOfWork.TagRepository.GetAllTagsAsync()).ToDictionary(t => t.NormalizedTitle);
        _collectionTags = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync(CollectionTagIncludes.SeriesMetadata))
                            .ToDictionary(t => t.NormalizedTitle);

    }

    public async Task ProcessSeriesAsync(IList<ParserInfo> parsedInfos, Library library, bool forceUpdate = false)
    {
        if (!parsedInfos.Any()) return;

        var seriesAdded = false;
        var scanWatch = Stopwatch.StartNew();
        var seriesName = parsedInfos[0].Series;
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Updated, seriesName));
        _logger.LogInformation("[ScannerService] Beginning series update on {SeriesName}", seriesName);

        // Check if there is a Series
        var firstInfo = parsedInfos[0];
        Series? series;
        try
        {
            series =
                await _unitOfWork.SeriesRepository.GetFullSeriesByAnyName(firstInfo.Series, firstInfo.LocalizedSeries,
                    library.Id, firstInfo.Format);
        }
        catch (Exception ex)
        {
            // TODO: Output more information to the user
            _logger.LogError(ex, "There was an exception finding existing series for {SeriesName} with Localized name of {LocalizedName} for library {LibraryId}. This indicates you have duplicate series with same name or localized name in the library. Correct this and rescan", firstInfo.Series, firstInfo.LocalizedSeries, library.Id);
            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent($"There was an exception finding existing series for {firstInfo.Series} with Localized name of {firstInfo.LocalizedSeries} for library {library.Id}",
                    "This indicates you have duplicate series with same name or localized name in the library. Correct this and rescan."));
            return;
        }

        if (series == null)
        {
            seriesAdded = true;
            series = new SeriesBuilder(firstInfo.Series)
                .WithLocalizedName(firstInfo.LocalizedSeries)
                .Build();
            _unitOfWork.SeriesRepository.Add(series);
        }

        if (series.LibraryId == 0) series.LibraryId = library.Id;

        try
        {
            _logger.LogInformation("[ScannerService] Processing series {SeriesName}", series.OriginalName);

            // parsedInfos[0] is not the first volume or chapter. We need to find it using a ComicInfo check (as it uses firstParsedInfo for series sort)
            var firstParsedInfo = parsedInfos.FirstOrDefault(p => p.ComicInfo != null, firstInfo);

            UpdateVolumes(series, parsedInfos, forceUpdate);
            series.Pages = series.Volumes.Sum(v => v.Pages);

            series.NormalizedName = series.Name.ToNormalized();
            series.OriginalName ??= firstParsedInfo.Series;
            if (series.Format == MangaFormat.Unknown)
            {
                series.Format = firstParsedInfo.Format;
            }

            if (string.IsNullOrEmpty(series.SortName))
            {
                series.SortName = series.Name;
            }
            if (!series.SortNameLocked)
            {
                series.SortName = series.Name;
                if (!string.IsNullOrEmpty(firstParsedInfo.SeriesSort))
                {
                    series.SortName = firstParsedInfo.SeriesSort;
                }
            }

            // parsedInfos[0] is not the first volume or chapter. We need to find it
            var localizedSeries = parsedInfos.Select(p => p.LocalizedSeries).FirstOrDefault(p => !string.IsNullOrEmpty(p));
            if (!series.LocalizedNameLocked && !string.IsNullOrEmpty(localizedSeries))
            {
                series.LocalizedName = localizedSeries;
                series.NormalizedLocalizedName = series.LocalizedName.ToNormalized();
            }

            UpdateSeriesMetadata(series, library);

            // Update series FolderPath here
            await UpdateSeriesFolderPath(parsedInfos, library, series);

            series.UpdateLastFolderScanned();

            if (_unitOfWork.HasChanges())
            {
                try
                {
                    await _unitOfWork.CommitAsync();
                }
                catch (Exception ex)
                {
                    await _unitOfWork.RollbackAsync();
                    _logger.LogCritical(ex,
                        "[ScannerService] There was an issue writing to the database for series {SeriesName}",
                        series.Name);
                    _logger.LogTrace("[ScannerService] Series Metadata Dump: {@Series}", series.Metadata);
                    _logger.LogTrace("[ScannerService] People Dump: {@People}", _people
                        .Select(p =>
                            new {p.Id, p.Name, SeriesMetadataIds =
                                p.SeriesMetadatas?.Select(m => m.Id),
                                ChapterMetadataIds =
                                    p.ChapterMetadatas?.Select(m => m.Id)
                                    .ToList()}));

                    await _eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent($"There was an issue writing to the DB for Series {series.OriginalName}",
                            ex.Message));
                    return;
                }

                // Process reading list after commit as we need to commit per list
                await _readingListService.CreateReadingListsFromSeries(series, library);

                if (seriesAdded)
                {
                    await _eventHub.SendMessageAsync(MessageFactory.SeriesAdded,
                        MessageFactory.SeriesAddedEvent(series.Id, series.Name, series.LibraryId), false);
                }

                _logger.LogInformation("[ScannerService] Finished series update on {SeriesName} in {Milliseconds} ms", seriesName, scanWatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] There was an exception updating series for {SeriesName}", series.Name);
        }

        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        await _metadataService.GenerateCoversForSeries(series, settings.EncodeMediaAs, settings.CoverImageSize);
        EnqueuePostSeriesProcessTasks(series.LibraryId, series.Id);
    }


    private async Task UpdateSeriesFolderPath(IEnumerable<ParserInfo> parsedInfos, Library library, Series series)
    {
        var seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(library.Folders.Select(l => l.Path),
            parsedInfos.Select(f => f.FullFilePath).ToList());
        if (seriesDirs.Keys.Count == 0)
        {
            _logger.LogCritical(
                "Scan Series has files spread outside a main series folder. This has negative performance effects. Please ensure all series are under a single folder from library");
            await _eventHub.SendMessageAsync(MessageFactory.Info,
                MessageFactory.InfoEvent($"{series.Name} has files spread outside a single series folder",
                    "This has negative performance effects. Please ensure all series are under a single folder from library"));
        }
        else
        {
            // Don't save FolderPath if it's a library Folder
            if (!library.Folders.Select(f => f.Path).Contains(seriesDirs.Keys.First()))
            {
                series.FolderPath = Parser.Parser.NormalizePath(seriesDirs.Keys.First());
                _logger.LogDebug("Updating {Series} FolderPath to {FolderPath}", series.Name, series.FolderPath);
            }
        }
    }

    public void EnqueuePostSeriesProcessTasks(int libraryId, int seriesId, bool forceUpdate = false)
    {
        BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanSeries(libraryId, seriesId, forceUpdate));
    }

    public void UpdateSeriesMetadata(Series series, Library library)
    {
        series.Metadata ??= new SeriesMetadataBuilder().Build();
        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);

        var firstFile = firstChapter?.Files.FirstOrDefault();
        if (firstFile == null) return;
        if (Parser.Parser.IsPdf(firstFile.FilePath)) return;

        var chapters = series.Volumes.SelectMany(volume => volume.Chapters).ToList();

        // Update Metadata based on Chapter metadata
        if (!series.Metadata.ReleaseYearLocked)
        {
            series.Metadata.ReleaseYear = chapters.MinimumReleaseYear();
        }

        // Set the AgeRating as highest in all the comicInfos
        if (!series.Metadata.AgeRatingLocked) series.Metadata.AgeRating = chapters.Max(chapter => chapter.AgeRating);

        // Count (aka expected total number of chapters or volumes from metadata) across all chapters
        series.Metadata.TotalCount = chapters.Max(chapter => chapter.TotalCount);
        // The actual number of count's defined across all chapter's metadata
        series.Metadata.MaxCount = chapters.Max(chapter => chapter.Count);

        var maxVolume = series.Volumes.Max(v => (int) Parser.Parser.MaxNumberFromRange(v.Name));
        var maxChapter = chapters.Max(c => (int) Parser.Parser.MaxNumberFromRange(c.Range));

        // Single books usually don't have a number in their Range (filename)
        if (series.Format == MangaFormat.Epub || series.Format == MangaFormat.Pdf && chapters.Count == 1)
        {
            series.Metadata.MaxCount = 1;
        } else if (series.Metadata.TotalCount <= 1 && chapters.Count == 1 && chapters[0].IsSpecial)
        {
            // If a series has a TotalCount of 1 (or no total count) and there is only a Special, mark it as Complete
            series.Metadata.MaxCount = series.Metadata.TotalCount;
        } else if ((maxChapter == 0 || maxChapter > series.Metadata.TotalCount) && maxVolume <= series.Metadata.TotalCount)
        {
            series.Metadata.MaxCount = maxVolume;
        } else if (maxVolume == series.Metadata.TotalCount)
        {
            series.Metadata.MaxCount = maxVolume;
        } else
        {
            series.Metadata.MaxCount = maxChapter;
        }

        if (!series.Metadata.PublicationStatusLocked)
        {
            series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
            if (series.Metadata.MaxCount == series.Metadata.TotalCount && series.Metadata.TotalCount > 0)
            {
                series.Metadata.PublicationStatus = PublicationStatus.Completed;
            } else if (series.Metadata.TotalCount > 0 && series.Metadata.MaxCount > 0)
            {
                series.Metadata.PublicationStatus = PublicationStatus.Ended;
            }
        }

        if (!string.IsNullOrEmpty(firstChapter?.Summary) && !series.Metadata.SummaryLocked)
        {
            series.Metadata.Summary = firstChapter.Summary;
        }

        if (!string.IsNullOrEmpty(firstChapter?.Language) && !series.Metadata.LanguageLocked)
        {
            series.Metadata.Language = firstChapter.Language;
        }

        if (!string.IsNullOrEmpty(firstChapter?.SeriesGroup) && library.ManageCollections)
        {
            _logger.LogDebug("Collection tag(s) found for {SeriesName}, updating collections", series.Name);
            foreach (var collection in firstChapter.SeriesGroup.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var normalizedName = Parser.Parser.Normalize(collection);
                if (!_collectionTags.TryGetValue(normalizedName, out var tag))
                {
                    tag = _collectionTagService.CreateTag(collection);
                    _collectionTags.Add(normalizedName, tag);
                }

                _collectionTagService.AddTagToSeriesMetadata(tag, series.Metadata);
            }
        }

        if (!series.Metadata.GenresLocked)
        {
            var genres = chapters.SelectMany(c => c.Genres).ToList();
            GenreHelper.KeepOnlySameGenreBetweenLists(series.Metadata.Genres.ToList(), genres, genre =>
            {
                series.Metadata.Genres.Remove(genre);
            });
        }


        #region People

        // Handle People
        foreach (var chapter in chapters)
        {
            if (!series.Metadata.WriterLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Writer))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.CoverArtistLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.CoverArtist))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.PublisherLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Publisher))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.CharacterLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Character))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.ColoristLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Colorist))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.EditorLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Editor))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.InkerLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Inker))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.LettererLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Letterer))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.PencillerLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Penciller))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.TranslatorLocked)
            {
                foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Translator))
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }
            }

            if (!series.Metadata.TagsLocked)
            {
                foreach (var tag in chapter.Tags)
                {
                    TagHelper.AddTagIfNotExists(series.Metadata.Tags, tag);
                }
            }

            if (!series.Metadata.GenresLocked)
            {
                foreach (var genre in chapter.Genres)
                {
                    GenreHelper.AddGenreIfNotExists(series.Metadata.Genres, genre);
                }
            }
        }
        // NOTE: The issue here is that people is just from chapter, but series metadata might already have some people on it
        // I might be able to filter out people that are in locked fields?
        var people = chapters.SelectMany(c => c.People).ToList();
        PersonHelper.KeepOnlySamePeopleBetweenLists(series.Metadata.People.ToList(),
            people, person =>
            {
                switch (person.Role)
                {
                    case PersonRole.Writer:
                        if (!series.Metadata.WriterLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Penciller:
                        if (!series.Metadata.PencillerLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Inker:
                        if (!series.Metadata.InkerLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Colorist:
                        if (!series.Metadata.ColoristLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Letterer:
                        if (!series.Metadata.LettererLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.CoverArtist:
                        if (!series.Metadata.CoverArtistLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Editor:
                        if (!series.Metadata.EditorLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Publisher:
                        if (!series.Metadata.PublisherLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Character:
                        if (!series.Metadata.CharacterLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Translator:
                        if (!series.Metadata.TranslatorLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Other:
                    default:
                        series.Metadata.People.Remove(person);
                        break;
                }
            });

        #endregion

    }

    public void UpdateVolumes(Series series, IList<ParserInfo> parsedInfos, bool forceUpdate = false)
    {
        // Add new volumes and update chapters per volume
        var distinctVolumes = parsedInfos.DistinctVolumes();
        _logger.LogDebug("[ScannerService] Updating {DistinctVolumes} volumes on {SeriesName}", distinctVolumes.Count, series.Name);
        foreach (var volumeNumber in distinctVolumes)
        {
            Volume? volume;
            try
            {
                volume = series.Volumes.SingleOrDefault(s => s.Name == volumeNumber);
            }
            catch (Exception ex)
            {
                if (!ex.Message.Equals("Sequence contains more than one matching element")) throw;
                _logger.LogCritical("[ScannerService] Kavita found corrupted volume entries on {SeriesName}. Please delete the series from Kavita via UI and rescan", series.Name);
                throw new KavitaException(
                    $"Kavita found corrupted volume entries on {series.Name}. Please delete the series from Kavita via UI and rescan");
            }
            if (volume == null)
            {
                volume = new VolumeBuilder(volumeNumber)
                    .WithSeriesId(series.Id)
                    .Build();
                series.Volumes.Add(volume);
            }

            volume.Name = volumeNumber;

            _logger.LogDebug("[ScannerService] Parsing {SeriesName} - Volume {VolumeNumber}", series.Name, volume.Name);
            var infos = parsedInfos.Where(p => p.Volumes == volumeNumber).ToArray();
            UpdateChapters(series, volume, infos, forceUpdate);
            volume.Pages = volume.Chapters.Sum(c => c.Pages);

            // Update all the metadata on the Chapters
            foreach (var chapter in volume.Chapters)
            {
                var firstFile = chapter.Files.MinBy(x => x.Chapter);
                if (firstFile == null || _cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) continue;
                try
                {
                    var firstChapterInfo = infos.SingleOrDefault(i => i.FullFilePath.Equals(firstFile.FilePath));
                    UpdateChapterFromComicInfo(chapter, firstChapterInfo?.ComicInfo, forceUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was some issue when updating chapter's metadata");
                }
            }
        }

        // Remove existing volumes that aren't in parsedInfos
        var nonDeletedVolumes = series.Volumes.Where(v => parsedInfos.Select(p => p.Volumes).Contains(v.Name)).ToList();
        if (series.Volumes.Count != nonDeletedVolumes.Count)
        {
            _logger.LogDebug("[ScannerService] Removed {Count} volumes from {SeriesName} where parsed infos were not mapping with volume name",
                (series.Volumes.Count - nonDeletedVolumes.Count), series.Name);
            var deletedVolumes = series.Volumes.Except(nonDeletedVolumes);
            foreach (var volume in deletedVolumes)
            {
                var file = volume.Chapters.FirstOrDefault()?.Files?.FirstOrDefault()?.FilePath ?? string.Empty;
                if (!string.IsNullOrEmpty(file) && _directoryService.FileSystem.File.Exists(file))
                {
                    _logger.LogInformation(
                        "[ScannerService] Volume cleanup code was trying to remove a volume with a file still existing on disk. File: {File}",
                        file);
                }

                _logger.LogDebug("[ScannerService] Removed {SeriesName} - Volume {Volume}: {File}", series.Name, volume.Name, file);
            }

            series.Volumes = nonDeletedVolumes;
        }
    }

    public void UpdateChapters(Series series, Volume volume, IList<ParserInfo> parsedInfos, bool forceUpdate = false)
    {
        // Add new chapters
        foreach (var info in parsedInfos)
        {
            // Specials go into their own chapters with Range being their filename and IsSpecial = True. Non-Specials with Vol and Chap as 0
            // also are treated like specials for UI grouping.
            Chapter? chapter;
            try
            {
                chapter = volume.Chapters.GetChapterByRange(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{FileName} mapped as '{Series} - Vol {Volume} Ch {Chapter}' is a duplicate, skipping", info.FullFilePath, info.Series, info.Volumes, info.Chapters);
                continue;
            }

            if (chapter == null)
            {
                _logger.LogDebug(
                    "[ScannerService] Adding new chapter, {Series} - Vol {Volume} Ch {Chapter}", info.Series, info.Volumes, info.Chapters);
                chapter = ChapterBuilder.FromParserInfo(info).Build();
                volume.Chapters.Add(chapter);
                series.UpdateLastChapterAdded();
            }
            else
            {
                chapter.UpdateFrom(info);
            }

            if (chapter == null) continue;
            // Add files
            var specialTreatment = info.IsSpecialInfo();
            AddOrUpdateFileForChapter(chapter, info, forceUpdate);
            chapter.Number = Parser.Parser.MinNumberFromRange(info.Chapters).ToString(CultureInfo.InvariantCulture);
            chapter.Range = specialTreatment ? info.Filename : info.Chapters;
        }


        // Remove chapters that aren't in parsedInfos or have no files linked
        var existingChapters = volume.Chapters.ToList();
        foreach (var existingChapter in existingChapters)
        {
            if (existingChapter.Files.Count == 0 || !parsedInfos.HasInfo(existingChapter))
            {
                _logger.LogDebug("[ScannerService] Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName}", existingChapter.Range, volume.Name, parsedInfos[0].Series);
                volume.Chapters.Remove(existingChapter);
            }
            else
            {
                // Ensure we remove any files that no longer exist AND order
                existingChapter.Files = existingChapter.Files
                    .Where(f => parsedInfos.Any(p => p.FullFilePath == f.FilePath))
                    .OrderByNatural(f => f.FilePath).ToList();
                existingChapter.Pages = existingChapter.Files.Sum(f => f.Pages);
            }
        }
    }

    public void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info, bool forceUpdate = false)
    {
        chapter.Files ??= new List<MangaFile>();
        var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
        var fileInfo = _directoryService.FileSystem.FileInfo.New(info.FullFilePath);
        if (existingFile != null)
        {
            existingFile.Format = info.Format;
            if (!forceUpdate && !_fileService.HasFileBeenModifiedSince(existingFile.FilePath, existingFile.LastModified) && existingFile.Pages != 0) return;
            existingFile.Pages = _readingItemService.GetNumberOfPages(info.FullFilePath, info.Format);
            existingFile.Extension = fileInfo.Extension.ToLowerInvariant();
            existingFile.Bytes = fileInfo.Length;
            // We skip updating DB here with last modified time so that metadata refresh can do it
        }
        else
        {

            var file = new MangaFileBuilder(info.FullFilePath, info.Format, _readingItemService.GetNumberOfPages(info.FullFilePath, info.Format))
                .WithExtension(fileInfo.Extension)
                .WithBytes(fileInfo.Length)
                .Build();
            chapter.Files.Add(file);
        }
    }

    public void UpdateChapterFromComicInfo(Chapter chapter, ComicInfo? comicInfo, bool forceUpdate = false)
    {
        if (comicInfo == null) return;
        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null ||
            _cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) return;

        _logger.LogTrace("[ScannerService] Read ComicInfo for {File}", firstFile.FilePath);

        chapter.AgeRating = ComicInfo.ConvertAgeRatingToEnum(comicInfo.AgeRating);

        if (!string.IsNullOrEmpty(comicInfo.Title))
        {
            chapter.TitleName = comicInfo.Title.Trim();
        }

        if (!string.IsNullOrEmpty(comicInfo.Summary))
        {
            chapter.Summary = comicInfo.Summary;
        }

        if (!string.IsNullOrEmpty(comicInfo.LanguageISO))
        {
            chapter.Language = comicInfo.LanguageISO;
        }

        if (!string.IsNullOrEmpty(comicInfo.SeriesGroup))
        {
            chapter.SeriesGroup = comicInfo.SeriesGroup;
        }

        if (!string.IsNullOrEmpty(comicInfo.StoryArc))
        {
            chapter.StoryArc = comicInfo.StoryArc;
        }

        if (!string.IsNullOrEmpty(comicInfo.AlternateSeries))
        {
            chapter.AlternateSeries = comicInfo.AlternateSeries;
        }

        if (!string.IsNullOrEmpty(comicInfo.AlternateNumber))
        {
            chapter.AlternateNumber = comicInfo.AlternateNumber;
        }

        if (!string.IsNullOrEmpty(comicInfo.StoryArcNumber))
        {
            chapter.StoryArcNumber = comicInfo.StoryArcNumber;
        }

        if (comicInfo.AlternateCount > 0)
        {
            chapter.AlternateCount = comicInfo.AlternateCount;
        }

        if (!string.IsNullOrEmpty(comicInfo.Web))
        {
            chapter.WebLinks = string.Join(",", comicInfo.Web
                .Split(",")
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s.Trim())
            );
        }

        if (!string.IsNullOrEmpty(comicInfo.Isbn))
        {
            chapter.ISBN = comicInfo.Isbn;
        }

        if (comicInfo.Count > 0)
        {
            chapter.TotalCount = comicInfo.Count;
        }

        // This needs to check against both Number and Volume to calculate Count
        chapter.Count = comicInfo.CalculatedCount();

        void AddPerson(Person person)
        {
            PersonHelper.AddPersonIfNotExists(chapter.People, person);
        }

        void AddGenre(Genre genre, bool newTag)
        {
            chapter.Genres.Add(genre);
        }

        void AddTag(Tag tag, bool added)
        {
            chapter.Tags.Add(tag);
        }


        if (comicInfo.Year > 0)
        {
            var day = Math.Max(comicInfo.Day, 1);
            var month = Math.Max(comicInfo.Month, 1);
            chapter.ReleaseDate = new DateTime(comicInfo.Year, month, day);
        }

        var people = GetTagValues(comicInfo.Colorist);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Colorist);
        UpdatePeople(people, PersonRole.Colorist, AddPerson);

        people = GetTagValues(comicInfo.Characters);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Character);
        UpdatePeople(people, PersonRole.Character, AddPerson);


        people = GetTagValues(comicInfo.Translator);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Translator);
        UpdatePeople(people, PersonRole.Translator, AddPerson);


        people = GetTagValues(comicInfo.Writer);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Writer);
        UpdatePeople(people, PersonRole.Writer, AddPerson);

        people = GetTagValues(comicInfo.Editor);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Editor);
        UpdatePeople(people, PersonRole.Editor, AddPerson);

        people = GetTagValues(comicInfo.Inker);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Inker);
        UpdatePeople(people, PersonRole.Inker, AddPerson);

        people = GetTagValues(comicInfo.Letterer);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Letterer);
        UpdatePeople(people, PersonRole.Letterer, AddPerson);

        people = GetTagValues(comicInfo.Penciller);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Penciller);
        UpdatePeople(people, PersonRole.Penciller, AddPerson);

        people = GetTagValues(comicInfo.CoverArtist);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.CoverArtist);
        UpdatePeople(people, PersonRole.CoverArtist, AddPerson);

        people = GetTagValues(comicInfo.Publisher);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Publisher);
        UpdatePeople(people, PersonRole.Publisher, AddPerson);

        var genres = GetTagValues(comicInfo.Genre);
        GenreHelper.KeepOnlySameGenreBetweenLists(chapter.Genres,
            genres.Select(g => new GenreBuilder(g).Build()).ToList());
        UpdateGenre(genres, AddGenre);

        var tags = GetTagValues(comicInfo.Tags);
        TagHelper.KeepOnlySameTagBetweenLists(chapter.Tags, tags.Select(t => new TagBuilder(t).Build()).ToList());
        UpdateTag(tags, AddTag);
    }

    private static IList<string> GetTagValues(string comicInfoTagSeparatedByComma)
    {
        // TODO: Move this to an extension and test it
        if (string.IsNullOrEmpty(comicInfoTagSeparatedByComma))
        {
            return ImmutableList<string>.Empty;
        }

        return comicInfoTagSeparatedByComma.Split(",")
            .Select(s => s.Trim())
            .DistinctBy(Parser.Parser.Normalize)
            .ToList();
    }

    /// <summary>
    /// Given a list of all existing people, this will check the new names and roles and if it doesn't exist in allPeople, will create and
    /// add an entry. For each person in name, the callback will be executed.
    /// </summary>
    /// <remarks>This does not remove people if an empty list is passed into names</remarks>
    /// <remarks>This is used to add new people to a list without worrying about duplicating rows in the DB</remarks>
    /// <param name="names"></param>
    /// <param name="role"></param>
    /// <param name="action"></param>
    private void UpdatePeople(IEnumerable<string> names, PersonRole role, Action<Person> action)
    {
        var allPeopleTypeRole = _people.Where(p => p.Role == role).ToList();

        foreach (var name in names)
        {
            var normalizedName = name.ToNormalized();
            var person = allPeopleTypeRole.Find(p =>
                p.NormalizedName != null && p.NormalizedName.Equals(normalizedName));

            if (person == null)
            {
                person = new PersonBuilder(name, role).Build();
                _people.Add(person);
            }
            action(person);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="names"></param>
    /// <param name="action">Executes for each tag</param>
    private void UpdateGenre(IEnumerable<string> names, Action<Genre, bool> action)
    {
        foreach (var name in names)
        {
            var normalizedName = name.ToNormalized();
            if (string.IsNullOrEmpty(normalizedName)) continue;

            _genres.TryGetValue(normalizedName, out var genre);
            var newTag = genre == null;
            if (newTag)
            {
                genre = new GenreBuilder(name).Build();
                _genres.Add(normalizedName, genre);
                _unitOfWork.GenreRepository.Attach(genre);
            }

            action(genre!, newTag);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="names"></param>
    /// <param name="action">Callback for every item. Will give said item back and a bool if item was added</param>
    private void UpdateTag(IEnumerable<string> names, Action<Tag, bool> action)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name.Trim())) continue;

            var normalizedName = name.ToNormalized();
            _tags.TryGetValue(normalizedName, out var tag);

            var added = tag == null;
            if (tag == null)
            {
                tag = new TagBuilder(name).Build();
                _tags.Add(normalizedName, tag);
            }

            action(tag, added);
        }
    }

}
