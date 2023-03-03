using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Parser;
using API.Services.Tasks.Metadata;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;

public interface IProcessSeries
{
    /// <summary>
    /// Do not allow this Prime to be invoked by multiple threads. It will break the DB.
    /// </summary>
    /// <returns></returns>
    Task Prime();
    Task ProcessSeriesAsync(IList<ParserInfo> parsedInfos, Library library, bool forceUpdate = false);
    void EnqueuePostSeriesProcessTasks(int libraryId, int seriesId, bool forceUpdate = false);
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

    private Dictionary<string, Genre> _genres;
    private IList<Person> _people;
    private Dictionary<string, Tag> _tags;
    private Dictionary<string, CollectionTag> _collectionTags;

    public ProcessSeries(IUnitOfWork unitOfWork, ILogger<ProcessSeries> logger, IEventHub eventHub,
        IDirectoryService directoryService, ICacheHelper cacheHelper, IReadingItemService readingItemService,
        IFileService fileService, IMetadataService metadataService, IWordCountAnalyzerService wordCountAnalyzerService,
        ICollectionTagService collectionTagService)
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

        _genres = new List<Genre>();
        _tags = new List<Tag>();
        _people = new List<Person>();
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
        var seriesName = parsedInfos.First().Series;
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Updated, seriesName));
        _logger.LogInformation("[ScannerService] Beginning series update on {SeriesName}", seriesName);

        // Check if there is a Series
        var firstInfo = parsedInfos.First();
        Series? series;
        try
        {
            series =
                await _unitOfWork.SeriesRepository.GetFullSeriesByAnyName(firstInfo.Series, firstInfo.LocalizedSeries,
                    library.Id, firstInfo.Format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception finding existing series for {SeriesName} with Localized name of {LocalizedName} for library {LibraryId}. This indicates you have duplicate series with same name or localized name in the library. Correct this and rescan", firstInfo.Series, firstInfo.LocalizedSeries, library.Id);
            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent($"There was an exception finding existing series for {firstInfo.Series} with Localized name of {firstInfo.LocalizedSeries} for library {library.Id}",
                    "This indicates you have duplicate series with same name or localized name in the library. Correct this and rescan."));
            return;
        }

        if (series == null)
        {
            seriesAdded = true;
            series = DbFactory.Series(firstInfo.Series, firstInfo.LocalizedSeries);
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

            //CreateReadingListsFromSeries(series, library); This will be implemented later when I solution it

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
                        "[ScannerService] There was an issue writing to the database for series {@SeriesName}",
                        series.Name);

                    await _eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent($"There was an issue writing to the DB for Series {series}",
                            ex.Message));
                    return;
                }

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

        await _metadataService.GenerateCoversForSeries(series, false);
        EnqueuePostSeriesProcessTasks(series.LibraryId, series.Id);
    }

    private void CreateReadingListsFromSeries(Series series, Library library)
    {
        //if (!library.ManageReadingLists) return;
        _logger.LogInformation("Generating Reading Lists for {SeriesName}", series.Name);

        series.Metadata ??= DbFactory.SeriesMetadata(new List<CollectionTag>());
        foreach (var chapter in series.Volumes.SelectMany(v => v.Chapters))
        {
            if (!string.IsNullOrEmpty(chapter.StoryArc))
            {
                var readingLists = chapter.StoryArc.Split(',');
                var readingListOrders = chapter.StoryArcNumber.Split(',');
                if (readingListOrders.Length == 0)
                {
                    _logger.LogDebug("[ScannerService] There are no StoryArc orders listed, all reading lists fueled from StoryArc will be unordered");

                }
            }
        }
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

    private void UpdateSeriesMetadata(Series series, Library library)
    {
        series.Metadata ??= DbFactory.SeriesMetadata(new List<CollectionTag>());
        var isBook = library.Type == LibraryType.Book;
        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, isBook);

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

        series.Metadata.TotalCount = chapters.Max(chapter => chapter.TotalCount);
        series.Metadata.MaxCount = chapters.Max(chapter => chapter.Count);
        // To not have to rely completely on ComicInfo, try to parse out if the series is complete by checking parsed filenames as well.
        if (series.Metadata.MaxCount != series.Metadata.TotalCount)
        {
            var maxVolume = series.Volumes.Max(v => (int) Parser.Parser.MaxNumberFromRange(v.Name));
            var maxChapter = chapters.Max(c => (int) Parser.Parser.MaxNumberFromRange(c.Range));
            if (maxVolume == series.Metadata.TotalCount) series.Metadata.MaxCount = maxVolume;
            else if (maxChapter == series.Metadata.TotalCount) series.Metadata.MaxCount = maxChapter;
        }


        if (!series.Metadata.PublicationStatusLocked)
        {
            series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
            if (series.Metadata.MaxCount >= series.Metadata.TotalCount && series.Metadata.TotalCount > 0)
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

            foreach (var collection in firstChapter.SeriesGroup.Split(','))
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

        var genres = chapters.SelectMany(c => c.Genres).ToList();
        GenreHelper.KeepOnlySameGenreBetweenLists(series.Metadata.Genres.ToList(), genres, genre =>
        {
            if (series.Metadata.GenresLocked) return;
            series.Metadata.Genres.Remove(genre);
        });

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
                    default:
                        series.Metadata.People.Remove(person);
                        break;
                }
            });
    }

    private void UpdateVolumes(Series series, IList<ParserInfo> parsedInfos, bool forceUpdate = false)
    {
        var startingVolumeCount = series.Volumes.Count;
        // Add new volumes and update chapters per volume
        var distinctVolumes = parsedInfos.DistinctVolumes();
        _logger.LogDebug("[ScannerService] Updating {DistinctVolumes} volumes on {SeriesName}", distinctVolumes.Count, series.Name);
        foreach (var volumeNumber in distinctVolumes)
        {
            _logger.LogDebug("[ScannerService] Looking up volume for {VolumeNumber}", volumeNumber);
            Volume? volume;
            try
            {
                volume = series.Volumes.SingleOrDefault(s => s.Name == volumeNumber);
            }
            catch (Exception ex)
            {
                if (ex.Message.Equals("Sequence contains more than one matching element"))
                {
                    _logger.LogCritical("[ScannerService] Kavita found corrupted volume entries on {SeriesName}. Please delete the series from Kavita via UI and rescan", series.Name);
                    throw new KavitaException(
                        $"Kavita found corrupted volume entries on {series.Name}. Please delete the series from Kavita via UI and rescan");
                }
                throw;
            }
            if (volume == null)
            {
                volume = DbFactory.Volume(volumeNumber);
                volume.SeriesId = series.Id;
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
                if (firstFile == null || _cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, firstFile)) continue;
                try
                {
                    var firstChapterInfo = infos.SingleOrDefault(i => i.FullFilePath.Equals(firstFile.FilePath));
                    UpdateChapterFromComicInfo(chapter, firstChapterInfo?.ComicInfo);
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

        // DO I need this anymore?
        _logger.LogDebug("[ScannerService] Updated {SeriesName} volumes from count of {StartingVolumeCount} to {VolumeCount}",
            series.Name, startingVolumeCount, series.Volumes.Count);
    }

    private void UpdateChapters(Series series, Volume volume, IList<ParserInfo> parsedInfos, bool forceUpdate = false)
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
                chapter = DbFactory.Chapter(info);
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
            chapter.Number = Parser.Parser.MinNumberFromRange(info.Chapters) + string.Empty;
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

    private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info, bool forceUpdate = false)
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
            var file = DbFactory.MangaFile(info.FullFilePath, info.Format, _readingItemService.GetNumberOfPages(info.FullFilePath, info.Format));
            if (file == null) return;
            file.Extension = fileInfo.Extension.ToLowerInvariant();
            file.Bytes = fileInfo.Length;
            chapter.Files.Add(file);
        }
    }

    private void UpdateChapterFromComicInfo(Chapter chapter, ComicInfo? info)
    {
        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null ||
            _cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, firstFile)) return;

        var comicInfo = info;
        if (info == null)
        {
            comicInfo = _readingItemService.GetComicInfo(firstFile.FilePath);
        }

        if (comicInfo == null) return;
        _logger.LogDebug("[ScannerService] Read ComicInfo for {File}", firstFile.FilePath);

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
        UpdatePeople(people, PersonRole.Colorist,
            AddPerson);

        people = GetTagValues(comicInfo.Characters);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Character);
        UpdatePeople(people, PersonRole.Character,
            AddPerson);


        people = GetTagValues(comicInfo.Translator);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Translator);
        UpdatePeople(people, PersonRole.Translator,
            AddPerson);


        people = GetTagValues(comicInfo.Writer);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Writer);
        UpdatePeople(people, PersonRole.Writer,
            AddPerson);

        people = GetTagValues(comicInfo.Editor);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Editor);
        UpdatePeople(people, PersonRole.Editor,
            AddPerson);

        people = GetTagValues(comicInfo.Inker);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Inker);
        UpdatePeople(people, PersonRole.Inker,
            AddPerson);

        people = GetTagValues(comicInfo.Letterer);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Letterer);
        UpdatePeople(people, PersonRole.Letterer,
            AddPerson);


        people = GetTagValues(comicInfo.Penciller);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Penciller);
        UpdatePeople(people, PersonRole.Penciller,
            AddPerson);

        people = GetTagValues(comicInfo.CoverArtist);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.CoverArtist);
        UpdatePeople(people, PersonRole.CoverArtist,
            AddPerson);

        people = GetTagValues(comicInfo.Publisher);
        PersonHelper.RemovePeople(chapter.People, people, PersonRole.Publisher);
        UpdatePeople(people, PersonRole.Publisher,
            AddPerson);

        var genres = GetTagValues(comicInfo.Genre);
        GenreHelper.KeepOnlySameGenreBetweenLists(chapter.Genres,
            genres.Select(DbFactory.Genre).ToList());
        UpdateGenre(genres, AddGenre);

        var tags = GetTagValues(comicInfo.Tags);
        TagHelper.KeepOnlySameTagBetweenLists(chapter.Tags, tags.Select(DbFactory.Tag).ToList());
        UpdateTag(tags, AddTag);
    }

    private static IList<string> GetTagValues(string comicInfoTagSeparatedByComma)
    {

        if (!string.IsNullOrEmpty(comicInfoTagSeparatedByComma))
        {
            return comicInfoTagSeparatedByComma.Split(",").Select(s => s.Trim()).DistinctBy(Parser.Parser.Normalize).ToList();
        }
        return ImmutableList<string>.Empty;
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
            var person = allPeopleTypeRole.FirstOrDefault(p =>
                p.NormalizedName != null && p.NormalizedName.Equals(normalizedName));
            if (person == null)
            {
                person = DbFactory.Person(name, role);
                lock (_people)
                {
                    _people.Add(person);
                }
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
                genre = DbFactory.Genre(name);
                lock (_genres)
                {
                    _genres.Add(normalizedName, genre);
                    _unitOfWork.GenreRepository.Attach(genre);
                }
            }

            action(genre, newTag);
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
                tag = DbFactory.Tag(name);
                lock (_tags)
                {
                    _tags.Add(normalizedName, tag);
                }
            }

            action(tag, added);
        }
    }

}
