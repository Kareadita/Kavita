using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
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

    void Reset();
    Task ProcessSeriesAsync(IList<ParserInfo> parsedInfos, Library library, int totalToProcess, bool forceUpdate = false);
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
    private readonly IReadingListService _readingListService;
    private readonly IExternalMetadataService _externalMetadataService;
    private readonly ITagManagerService _tagManagerService;


    public ProcessSeries(IUnitOfWork unitOfWork, ILogger<ProcessSeries> logger, IEventHub eventHub,
        IDirectoryService directoryService, ICacheHelper cacheHelper, IReadingItemService readingItemService,
        IFileService fileService, IMetadataService metadataService, IWordCountAnalyzerService wordCountAnalyzerService,
        IReadingListService readingListService,
        IExternalMetadataService externalMetadataService)
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
        _readingListService = readingListService;
        _externalMetadataService = externalMetadataService;
        _tagManagerService = new TagManagerService(_unitOfWork, _logger);
    }

    /// <summary>
    /// Invoke this before processing any series, just once to prime all the needed data during a scan
    /// </summary>
    public async Task Prime()
    {
        try
        {
            await _tagManagerService.Prime();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unable to prime tag manager. Scan cannot proceed. Report to Kavita dev");
        }
    }

    /// <summary>
    /// Frees up memory
    /// </summary>
    public void Reset()
    {
        _tagManagerService.Reset();
    }

    public async Task ProcessSeriesAsync(IList<ParserInfo> parsedInfos, Library library, int totalToProcess, bool forceUpdate = false)
    {
        if (!parsedInfos.Any()) return;

        var seriesAdded = false;
        var scanWatch = Stopwatch.StartNew();
        var seriesName = parsedInfos[0].Series;
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Updated, seriesName, totalToProcess));
        _logger.LogInformation("[ScannerService] Beginning series update on {SeriesName}, Forced: {ForceUpdate}", seriesName, forceUpdate);

        // Check if there is a Series
        var firstInfo = parsedInfos[0];
        Series? series;
        try
        {
            // There is an opportunity to allow duplicate series here. Like if One is in root/marvel/batman and another is root/dc/batman
            // by changing to a ToList() and if multiple, doing a firstInfo.FirstFolder/RootFolder type check
            series =
                await _unitOfWork.SeriesRepository.GetFullSeriesByAnyName(firstInfo.Series, firstInfo.LocalizedSeries,
                    library.Id, firstInfo.Format);
        }
        catch (Exception ex)
        {
            await ReportDuplicateSeriesLookup(library, firstInfo, ex);
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

            await UpdateVolumes(series, parsedInfos, forceUpdate);
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

            await UpdateSeriesMetadata(series, library);

            // Update series FolderPath here
            await UpdateSeriesFolderPath(parsedInfos, library, series);

            series.UpdateLastFolderScanned();

            if (_unitOfWork.HasChanges())
            {
                try
                {
                    await _unitOfWork.CommitAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is Series)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = await entry.GetDatabaseValuesAsync();

                            foreach (var property in proposedValues.Properties)
                            {
                                var proposedValue = proposedValues[property];
                                var databaseValue = databaseValues[property];

                                // TODO: decide which value should be written to database
                                _logger.LogDebug("Property conflict, proposed: {Proposed} vs db: {Database}", proposedValue, databaseValue);
                                // proposedValues[property] = <value to be saved>;
                            }

                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                    }


                    _logger.LogCritical(ex,
                        "[ScannerService] There was an issue writing to the database for series {SeriesName}",
                        series.Name);
                    await _eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent($"There was an issue writing to the DB for Series {series.OriginalName}",
                            ex.Message));
                    return;
                }
                catch (Exception ex)
                {
                    await _unitOfWork.RollbackAsync();
                    _logger.LogCritical(ex,
                        "[ScannerService] There was an issue writing to the database for series {SeriesName}",
                        series.Name);

                    await _eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent($"There was an issue writing to the DB for Series {series.OriginalName}",
                            ex.Message));
                    return;
                }


                // Process reading list after commit as we need to commit per list
                await _readingListService.CreateReadingListsFromSeries(library.Id, series.Id);

                if (seriesAdded)
                {
                    // See if any recommendations can link up to the series and pre-fetch external metadata for the series
                    _logger.LogInformation("Linking up External Recommendations new series (if applicable)");

                    // BackgroundJob.Enqueue(() =>
                    //     _externalMetadataService.GetNewSeriesData(series.Id, series.Library.Type));
                    await _externalMetadataService.GetNewSeriesData(series.Id, series.Library.Type);

                    await _eventHub.SendMessageAsync(MessageFactory.SeriesAdded,
                        MessageFactory.SeriesAddedEvent(series.Id, series.Name, series.LibraryId), false);
                }
                else
                {
                    await _unitOfWork.ExternalSeriesMetadataRepository.LinkRecommendationsToSeries(series);
                }

                _logger.LogInformation("[ScannerService] Finished series update on {SeriesName} in {Milliseconds} ms", seriesName, scanWatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] There was an exception updating series for {SeriesName}", series.Name);
            return;
        }

        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        await _metadataService.GenerateCoversForSeries(series, settings.EncodeMediaAs, settings.CoverImageSize);
        // BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanSeries(series.LibraryId, series.Id, forceUpdate));
        await _wordCountAnalyzerService.ScanSeries(series.LibraryId, series.Id, forceUpdate);
    }


    private async Task ReportDuplicateSeriesLookup(Library library, ParserInfo firstInfo, Exception ex)
    {
        var seriesCollisions = await _unitOfWork.SeriesRepository.GetAllSeriesByAnyName(firstInfo.LocalizedSeries, string.Empty, library.Id, firstInfo.Format);

        seriesCollisions = seriesCollisions.Where(collision =>
            collision.Name != firstInfo.Series || collision.LocalizedName != firstInfo.LocalizedSeries).ToList();

        if (seriesCollisions.Count > 1)
        {
            var firstCollision = seriesCollisions[0];
            var secondCollision = seriesCollisions[1];

            var tableRows = $"<tr><td>Name: {firstCollision.Name}</td><td>Name: {secondCollision.Name}</td></tr>" +
                            $"<tr><td>Localized: {firstCollision.LocalizedName}</td><td>Localized: {secondCollision.LocalizedName}</td></tr>" +
                            $"<tr><td>Filename: {Parser.Parser.NormalizePath(firstCollision.FolderPath)}</td><td>Filename: {Parser.Parser.NormalizePath(secondCollision.FolderPath)}</td></tr>";

            var htmlTable = $"<table class='table table-striped'><thead><tr><th>Series 1</th><th>Series 2</th></tr></thead><tbody>{string.Join(string.Empty, tableRows)}</tbody></table>";

            _logger.LogError(ex, "Scanner found a Series {SeriesName} which matched another Series {LocalizedName} in a different folder parallel to Library {LibraryName} root folder. This is not allowed. Please correct",
                firstInfo.Series, firstInfo.LocalizedSeries, library.Name);

            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent($"Library {library.Name} Series collision on {firstInfo.Series}",
                    htmlTable));
        }
    }


    private async Task UpdateSeriesFolderPath(IEnumerable<ParserInfo> parsedInfos, Library library, Series series)
    {
        var libraryFolders = library.Folders.Select(l => Parser.Parser.NormalizePath(l.Path)).ToList();
        var seriesFiles = parsedInfos.Select(f => Parser.Parser.NormalizePath(f.FullFilePath)).ToList();
        var seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryFolders, seriesFiles);
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
                // BUG: FolderPath can be a level higher than it needs to be. I'm not sure why it's like this, but I thought it should be one level lower.
                // I think it's like this because higher level is checked or not checked. But i think we can do both
                series.FolderPath = Parser.Parser.NormalizePath(seriesDirs.Keys.First());
                _logger.LogDebug("Updating {Series} FolderPath to {FolderPath}", series.Name, series.FolderPath);
            }
        }

        var lowestFolder = _directoryService.FindLowestDirectoriesFromFiles(libraryFolders, seriesFiles);
        if (!string.IsNullOrEmpty(lowestFolder))
        {
            series.LowestFolderPath = lowestFolder;
            _logger.LogDebug("Updating {Series} LowestFolderPath to {FolderPath}", series.Name, series.LowestFolderPath);
        }
    }


    private async Task UpdateSeriesMetadata(Series series, Library library)
    {
        series.Metadata ??= new SeriesMetadataBuilder().Build();
        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);

        var firstFile = firstChapter?.Files.FirstOrDefault();
        if (firstFile == null || Parser.Parser.IsPdf(firstFile.FilePath)) return;

        var chapters = series.Volumes.SelectMany(volume => volume.Chapters).ToList();

        // Update Metadata based on Chapter metadata
        if (!series.Metadata.ReleaseYearLocked)
        {
            series.Metadata.ReleaseYear = chapters.MinimumReleaseYear();
        }

        // Set the AgeRating as highest in all the comicInfos
        if (!series.Metadata.AgeRatingLocked) series.Metadata.AgeRating = chapters.Max(chapter => chapter.AgeRating);

        DeterminePublicationStatus(series, chapters);

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
            // Get the default admin to associate these tags to
            var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser(AppUserIncludes.Collections);
            if (defaultAdmin == null) return;

            _logger.LogDebug("Collection tag(s) found for {SeriesName}, updating collections", series.Name);
            foreach (var collection in firstChapter.SeriesGroup.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var (tag, _) = await _tagManagerService.GetCollectionTag(collection, defaultAdmin);
                if (tag == null) continue;

                // Check if the Series is already on the tag
                if (tag.Items.Any(s => s.MatchesSeriesByName(series.NormalizedName, series.NormalizedLocalizedName)))
                {
                    continue;
                }

                tag.Items.Add(series);
                await _unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(tag);
            }
        }

        if (!series.Metadata.GenresLocked)
        {
            // TODO: Refactor to be closer to how People works
            var genres = chapters.SelectMany(c => c.Genres).ToList();
            GenreHelper.KeepOnlySameGenreBetweenLists(series.Metadata.Genres.ToList(), genres, genre =>
            {
                series.Metadata.Genres.Remove(genre);
            });
        }



        //     if (!series.Metadata.TagsLocked)
        //     {
        // foreach (var tag in chapter.Tags)
        // {
        //     TagHelper.AddTagIfNotExists(series.Metadata.Tags, tag);
        // }
        //     }
        //
        //     if (!series.Metadata.GenresLocked)
        //     {
        //         foreach (var genre in chapter.Genres)
        //         {
        //             GenreHelper.AddGenreIfNotExists(series.Metadata.Genres, genre);
        //         }
        //     }

        // TODO: Implement People Support
        #region People
        // // Handle People
        foreach (var chapter in chapters)
        {
            if (!series.Metadata.WriterLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Writer).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Writer);
            }

            if (!series.Metadata.ColoristLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Colorist).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Colorist);
            }

            if (!series.Metadata.PublisherLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Publisher).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Publisher);
            }

            if (!series.Metadata.CoverArtistLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.CoverArtist).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.CoverArtist);
            }

            if (!series.Metadata.CharacterLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Character).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Character);
            }

            if (!series.Metadata.EditorLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Editor).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Editor);
            }

            if (!series.Metadata.InkerLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Inker).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Inker);
            }

            if (!series.Metadata.ImprintLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Imprint).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Imprint);
            }

            if (!series.Metadata.TeamLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Team).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Team);
            }

            if (!series.Metadata.LocationLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Location).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Location);
            }

            if (!series.Metadata.LettererLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Letterer).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Letterer);
            }

            if (!series.Metadata.PencillerLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Penciller).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Penciller);
            }

            if (!series.Metadata.TranslatorLocked)
            {
                var chapterPeople = chapter.People.Where(p => p.Role == PersonRole.Translator).ToList();
                UpdateSeriesMetadataPeople(series.Metadata, series.Metadata.People, chapterPeople, PersonRole.Translator);
            }


            if (!series.Metadata.TagsLocked)
            {
                // TODO: Refactor to be closer to how People works
                foreach (var tag in chapter.Tags) // This needs removal code
                {
                    TagHelper.AddTagIfNotExists(series.Metadata.Tags, tag);
                }
            }

            if (!series.Metadata.GenresLocked)
            {
                // TODO: Refactor to be closer to how People works
                foreach (var genre in chapter.Genres)
                {
                    GenreHelper.AddGenreIfNotExists(series.Metadata.Genres, genre);
                }
            }
        }

        //
        //     if (!series.Metadata.CoverArtistLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.CoverArtist))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.PublisherLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Publisher))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.CharacterLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Character))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.ColoristLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Colorist))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.EditorLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Editor))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.InkerLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Inker))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.ImprintLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Imprint))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.TeamLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Team))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.LocationLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Location))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.LettererLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Letterer))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.PencillerLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Penciller))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.TranslatorLocked)
        //     {
        //         foreach (var person in chapter.People.Where(p => p.Role == PersonRole.Translator))
        //         {
        //             PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
        //         }
        //     }
        //
        //     if (!series.Metadata.TagsLocked)
        //     {
        //         foreach (var tag in chapter.Tags)
        //         {
        //             TagHelper.AddTagIfNotExists(series.Metadata.Tags, tag);
        //         }
        //     }
        //
        //     if (!series.Metadata.GenresLocked)
        //     {
        //         foreach (var genre in chapter.Genres)
        //         {
        //             GenreHelper.AddGenreIfNotExists(series.Metadata.Genres, genre);
        //         }
        //     }
        // }
        // // NOTE: The issue here is that people is just from chapter, but series metadata might already have some people on it
        // // I might be able to filter out people that are in locked fields?
        // var people = chapters.SelectMany(c => c.People).ToList();
        // PersonHelper.KeepOnlySamePeopleBetweenLists(series.Metadata.People.ToList(),
        //     people, person =>
        //     {
        //         switch (person.Role)
        //         {
        //             case PersonRole.Writer:
        //                 if (!series.Metadata.WriterLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Penciller:
        //                 if (!series.Metadata.PencillerLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Inker:
        //                 if (!series.Metadata.InkerLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Imprint:
        //                 if (!series.Metadata.ImprintLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Colorist:
        //                 if (!series.Metadata.ColoristLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Letterer:
        //                 if (!series.Metadata.LettererLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.CoverArtist:
        //                 if (!series.Metadata.CoverArtistLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Editor:
        //                 if (!series.Metadata.EditorLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Publisher:
        //                 if (!series.Metadata.PublisherLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Character:
        //                 if (!series.Metadata.CharacterLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Translator:
        //                 if (!series.Metadata.TranslatorLocked) series.Metadata.People.Remove(person);
        //                 break;
        //             case PersonRole.Other:
        //             default:
        //                 series.Metadata.People.Remove(person);
        //                 break;
        //         }
        //     });
        //
        #endregion

    }


    private static void UpdateSeriesMetadataPeople(SeriesMetadata metadata, ICollection<SeriesMetadataPeople> metadataPeople, IEnumerable<ChapterPeople> chapterPeople, PersonRole role)
    {
        // Normalize and group by person
        var peopleToAdd = chapterPeople
            .Where(cp => cp.Role == role)
            .Select(cp => cp.Person)
            .ToList();

        // Remove any people who are not part of the new list
        var peopleToRemove = metadataPeople
            .Where(mp => mp.Role == role && peopleToAdd.All(p => p.NormalizedName != mp.Person.NormalizedName))
            .ToList();

        foreach (var personToRemove in peopleToRemove)
        {
            metadataPeople.Remove(personToRemove);
        }

        // Add new people if they do not already exist
        foreach (var person in peopleToAdd)
        {
            var existingPerson = metadataPeople
                .FirstOrDefault(mp => mp.Person.NormalizedName == person.NormalizedName && mp.Role == role);

            if (existingPerson == null)
            {
                metadataPeople.Add(new SeriesMetadataPeople
                {
                    PersonId = person.Id,
                    SeriesMetadataId = metadata.Id,
                    Role = role
                });
            }
        }
    }

    private void DeterminePublicationStatus(Series series, List<Chapter> chapters)
    {
        try
        {
            // Count (aka expected total number of chapters or volumes from metadata) across all chapters
            series.Metadata.TotalCount = chapters.Max(chapter => chapter.TotalCount);
            // The actual number of count's defined across all chapter's metadata
            series.Metadata.MaxCount = chapters.Max(chapter => chapter.Count);

            var nonSpecialVolumes = series.Volumes.Where(v => v.MaxNumber.IsNot(Parser.Parser.SpecialVolumeNumber));

            var maxVolume = (int) (nonSpecialVolumes.Any() ? nonSpecialVolumes.Max(v => v.MaxNumber) : 0);
            var maxChapter = (int) chapters.Max(c => c.MaxNumber);

            // Single books usually don't have a number in their Range (filename)
            if (series.Format == MangaFormat.Epub || series.Format == MangaFormat.Pdf && chapters.Count == 1)
            {
                series.Metadata.MaxCount = 1;
            }
            else if (series.Metadata.TotalCount <= 1 && chapters.Count == 1 && chapters[0].IsSpecial)
            {
                // If a series has a TotalCount of 1 (or no total count) and there is only a Special, mark it as Complete
                series.Metadata.MaxCount = series.Metadata.TotalCount;
            }
            else if ((maxChapter == Parser.Parser.DefaultChapterNumber || maxChapter > series.Metadata.TotalCount) &&
                     maxVolume <= series.Metadata.TotalCount)
            {
                series.Metadata.MaxCount = maxVolume;
            }
            else if (maxVolume == series.Metadata.TotalCount)
            {
                series.Metadata.MaxCount = maxVolume;
            }
            else
            {
                series.Metadata.MaxCount = maxChapter;
            }

            if (!series.Metadata.PublicationStatusLocked)
            {
                series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
                if (series.Metadata.MaxCount == series.Metadata.TotalCount && series.Metadata.TotalCount > 0)
                {
                    series.Metadata.PublicationStatus = PublicationStatus.Completed;
                }
                else if (series.Metadata.TotalCount > 0 && series.Metadata.MaxCount > 0)
                {
                    series.Metadata.PublicationStatus = PublicationStatus.Ended;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an issue determining Publication Status");
            series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
        }
    }

    private async Task UpdateVolumes(Series series, IList<ParserInfo> parsedInfos, bool forceUpdate = false)
    {
        // Add new volumes and update chapters per volume
        var distinctVolumes = parsedInfos.DistinctVolumes();
        _logger.LogDebug("[ScannerService] Updating {DistinctVolumes} volumes on {SeriesName}", distinctVolumes.Count, series.Name);
        foreach (var volumeNumber in distinctVolumes)
        {
            Volume? volume;
            try
            {
                // With the Name change to be formatted, Name no longer working because Name returns "1" and volumeNumber is "1.0", so we use LookupName as the original
                volume = series.Volumes.SingleOrDefault(s => s.LookupName == volumeNumber);
            }
            catch (Exception ex)
            {
                // TODO: Push this to UI in some way
                if (!ex.Message.Equals("Sequence contains more than one matching element")) throw;
                _logger.LogCritical(ex, "[ScannerService] Kavita found corrupted volume entries on {SeriesName}. Please delete the series from Kavita via UI and rescan", series.Name);
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

            volume.LookupName = volumeNumber;
            volume.Name = volume.GetNumberTitle();

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
                    await UpdateChapterFromComicInfo(chapter, firstChapterInfo?.ComicInfo, forceUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was some issue when updating chapter's metadata");
                }
            }
        }

        // Remove existing volumes that aren't in parsedInfos
        var nonDeletedVolumes = series.Volumes
            .Where(v => parsedInfos.Select(p => p.Volumes).Contains(v.LookupName))
            .ToList();
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
                    // This can happen when file is renamed and volume is removed
                    _logger.LogInformation(
                        "[ScannerService] Volume cleanup code was trying to remove a volume with a file still existing on disk (usually volume marker removed) File: {File}",
                        file);
                }

                _logger.LogDebug("[ScannerService] Removed {SeriesName} - Volume {Volume}: {File}", series.Name, volume.Name, file);
            }

            series.Volumes = nonDeletedVolumes;
        }
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
                chapter = ChapterBuilder.FromParserInfo(info).Build();
                volume.Chapters.Add(chapter);
                series.UpdateLastChapterAdded();
            }
            else
            {
                chapter.UpdateFrom(info);
            }

            if (chapter == null)
            {
                continue;
            }
            // Add files
            AddOrUpdateFileForChapter(chapter, info, forceUpdate);

            // TODO: Investigate using the ChapterBuilder here
            chapter.Number = Parser.Parser.MinNumberFromRange(info.Chapters).ToString(CultureInfo.InvariantCulture);
            chapter.MinNumber = Parser.Parser.MinNumberFromRange(info.Chapters);
            chapter.MaxNumber = Parser.Parser.MaxNumberFromRange(info.Chapters);
            chapter.Range = chapter.GetNumberTitle();

            if (!chapter.SortOrderLocked)
            {
                chapter.SortOrder = info.IssueOrder;
            }

            if (float.TryParse(chapter.Title, out _))
            {
                // If we have float based chapters, first scan can have the chapter formatted as Chapter 0.2 - .2 as the title is wrong.
                chapter.Title = chapter.GetNumberTitle();
            }

        }


        // Remove chapters that aren't in parsedInfos or have no files linked
        var existingChapters = volume.Chapters.ToList();
        foreach (var existingChapter in existingChapters)
        {
            if (existingChapter.Files.Count == 0 || !parsedInfos.HasInfo(existingChapter))
            {
                _logger.LogDebug("[ScannerService] Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName}",
                    existingChapter.Range, volume.Name, parsedInfos[0].Series);
                volume.Chapters.Remove(existingChapter);
            }
            else
            {
                // Ensure we remove any files that no longer exist AND order
                existingChapter.Files = existingChapter.Files
                    .Where(f => parsedInfos.Any(p => Parser.Parser.NormalizePath(p.FullFilePath) == Parser.Parser.NormalizePath(f.FilePath)))
                    .OrderByNatural(f => f.FilePath)
                    .ToList();
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
            existingFile.FileName = Parser.Parser.RemoveExtensionIfSupported(existingFile.FilePath);
            existingFile.FilePath = Parser.Parser.NormalizePath(existingFile.FilePath);
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

    private async Task UpdateChapterFromComicInfo(Chapter chapter, ComicInfo? comicInfo, bool forceUpdate = false)
    {
        if (comicInfo == null) return;
        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null ||
            _cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) return;

        _logger.LogTrace("[ScannerService] Read ComicInfo for {File}", firstFile.FilePath);

        if (!chapter.AgeRatingLocked)
        {
            chapter.AgeRating = ComicInfo.ConvertAgeRatingToEnum(comicInfo.AgeRating);
        }

        if (!chapter.TitleNameLocked && !string.IsNullOrEmpty(comicInfo.Title))
        {
            chapter.TitleName = comicInfo.Title.Trim();
        }

        if (!chapter.SummaryLocked && !string.IsNullOrEmpty(comicInfo.Summary))
        {
            chapter.Summary = comicInfo.Summary;
        }

        if (!chapter.LanguageLocked && !string.IsNullOrEmpty(comicInfo.LanguageISO))
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
                .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            );

            // For each weblink, try to parse out some MetadataIds and store in the Chapter directly for matching (CBL)
        }

        if (!chapter.ISBNLocked && !string.IsNullOrEmpty(comicInfo.Isbn))
        {
            chapter.ISBN = comicInfo.Isbn;
        }

        if (comicInfo.Count > 0)
        {
            chapter.TotalCount = comicInfo.Count;
        }

        // This needs to check against both Number and Volume to calculate Count
        chapter.Count = comicInfo.CalculatedCount();


        if (!chapter.ReleaseDateLocked && comicInfo.Year > 0)
        {
            var day = Math.Max(comicInfo.Day, 1);
            var month = Math.Max(comicInfo.Month, 1);
            chapter.ReleaseDate = new DateTime(comicInfo.Year, month, day);
        }

        if (!chapter.ColoristLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Colorist);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Writer);
            await UpdatePeople(chapter, people, PersonRole.Colorist);
        }

        if (!chapter.CharacterLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Characters);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Character);
            await UpdatePeople(chapter, people, PersonRole.Character);
        }


        if (!chapter.TranslatorLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Translator);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Translator);
            await UpdatePeople(chapter, people, PersonRole.Translator);
        }

        if (!chapter.WriterLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Writer);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Writer);
            await UpdatePeople(chapter, people, PersonRole.Writer);
        }

        if (!chapter.EditorLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Editor);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Editor);
            await UpdatePeople(chapter, people, PersonRole.Editor);
        }

        if (!chapter.InkerLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Inker);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Inker);
            await UpdatePeople(chapter, people, PersonRole.Inker);
        }

        if (!chapter.LettererLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Letterer);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Letterer);
            await UpdatePeople(chapter, people, PersonRole.Letterer);
        }

        if (!chapter.PencillerLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Penciller);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Penciller);
            await UpdatePeople(chapter, people, PersonRole.Penciller);
        }

        if (!chapter.CoverArtistLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.CoverArtist);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.CoverArtist);
            await UpdatePeople(chapter, people, PersonRole.CoverArtist);
        }

        if (!chapter.PublisherLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Publisher);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Publisher);
            await UpdatePeople(chapter, people, PersonRole.Publisher);
        }

        if (!chapter.ImprintLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Imprint);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Imprint);
            await UpdatePeople(chapter, people, PersonRole.Imprint);
        }

        if (!chapter.TeamLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Teams);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Team);
            await UpdatePeople(chapter, people, PersonRole.Team);
        }

        if (!chapter.LocationLocked)
        {
            var people = TagHelper.GetTagValues(comicInfo.Locations);
            //await UpdateChapterPeopleAsync(chapter.People, people, PersonRole.Location);
            await UpdatePeople(chapter, people, PersonRole.Location);
        }


        if (!chapter.GenresLocked)
        {
            var genres = TagHelper.GetTagValues(comicInfo.Genre);
            GenreHelper.KeepOnlySameGenreBetweenLists(chapter.Genres,
                genres.Select(g => new GenreBuilder(g).Build()).ToList());
            foreach (var genre in genres)
            {
                var g = await _tagManagerService.GetGenre(genre);
                if (g == null) continue;
                chapter.Genres.Add(g);
            }
        }

        if (!chapter.TagsLocked)
        {
            var tags = TagHelper.GetTagValues(comicInfo.Tags);
            TagHelper.KeepOnlySameTagBetweenLists(chapter.Tags, tags.Select(t => new TagBuilder(t).Build()).ToList());
            foreach (var tag in tags)
            {
                var t = await _tagManagerService.GetTag(tag);
                if (t == null) continue;
                chapter.Tags.Add(t);
            }
        }
    }

    private async Task UpdatePeople(Chapter chapter, IList<string> people, PersonRole role)
    {
        var modification = false;
        try
        {
            // Normalize the input names for comparison
            var normalizedPeople = people.Select(p => p.ToNormalized()).ToList();

            // Get all existing ChapterPeople for the role
            var existingChapterPeople = chapter.People.Where(cp => cp.Role == role).ToList();

            // Remove people not in the new list
            foreach (var existingChapterPerson in existingChapterPeople)
            {
                if (!normalizedPeople.Contains(existingChapterPerson.Person.NormalizedName))
                {
                    chapter.People.Remove(existingChapterPerson);
                    _unitOfWork.PersonRepository.Remove(existingChapterPerson);
                    modification = true;
                }
            }

            // Add new people or existing ones if not already in the Chapter
            foreach (var personName in people)
            {
                //var person = await _unitOfWork.PersonRepository.GetPersonByName(personName, true);
                var person = await _unitOfWork.DataContext.Person.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.NormalizedName == personName.ToNormalized());

                // If the person doesn't exist, create a new Person entity
                if (person == null)
                {
                    person = new PersonBuilder(personName).Build();

                    modification = true;
                    _unitOfWork.DataContext.Person.Attach(person);
                    await _unitOfWork.CommitAsync();
                }

                // Check if the person with the specific role is already added to the chapter's People collection
                var existingChapterPerson = chapter.People
                    .FirstOrDefault(cp => cp.PersonId == person.Id && cp.Role == role);

                // Check if this person with the specific role already exists for the chapter
                if (existingChapterPerson == null)
                {
                    var chapterPerson = new ChapterPeople
                    {
                        PersonId = person.Id,
                        ChapterId = chapter.Id,
                        Role = role
                    };

                    chapter.People.Add(chapterPerson);
                    modification = true;
                }
            }

            // Commit the changes to remove and add people
            if (modification)
            {
                // _unitOfWork.DataContext.ChangeTracker.DetectChanges();
                // Console.WriteLine(_unitOfWork.DataContext.ChangeTracker.DebugView.LongView);
                await _unitOfWork.CommitAsync();
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] There was an issue adding/updating a person");
        }
    }



    private async Task UpdateChapterPeopleAsync(ICollection<ChapterPeople> existingChapterPeople, IEnumerable<string> people, PersonRole role, Action<ChapterPeople>? action = null)
    {
        // Normalize the input people names
        var normalizedPeople = people.Select(Services.Tasks.Scanner.Parser.Parser.Normalize).ToList();

        // Find people that exist in ChapterPeople but are not in the new list
        var peopleToRemove = existingChapterPeople
            .Where(cp => cp.Role == role && !normalizedPeople.Contains(cp.Person.NormalizedName))
            .ToList();

        // Remove people that are no longer in the new list
        foreach (var chapterPerson in peopleToRemove)
        {
            existingChapterPeople.Remove(chapterPerson);
            action?.Invoke(chapterPerson);
        }

        // Add new people that are not already in the ChapterPeople collection
        foreach (var person in normalizedPeople)
        {
            // Check if the person already exists in the current ChapterPeople for the given role
            if (!existingChapterPeople.Any(cp => cp.Role == role && cp.Person.NormalizedName == person))
            {
                var newPerson = await _unitOfWork.PersonRepository.GetPersonByName(person);
                if (newPerson == null) continue;

                var chapterPerson = new ChapterPeople
                {
                    PersonId = newPerson.Id,
                    Person = newPerson,
                    Role = role
                };

                existingChapterPeople.Add(chapterPerson);
            }
        }
    }


}
