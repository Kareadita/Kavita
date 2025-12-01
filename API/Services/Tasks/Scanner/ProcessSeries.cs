using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.DTOs.KavitaPlus.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Entities.Person;
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
    Task<int?> ProcessSeriesAsync(MetadataSettingsDto settings, IList<ParserInfo> parsedInfos, ProcessSeriesArgs args);
}

public sealed record ProcessSeriesArgs
{
    public required Library Library { get; init; }
    public required int TotalToProcess { get; init; }
    public required int LeftToProcess { get; init; }
    public bool ForceUpdate { get; init; } = false;
}

internal sealed record UpdateChapterArgs
{
    public required MetadataSettingsDto Settings { get; init; }
    public required Series Series { get; init; }
    public required Volume Volume { get; init; }
    public required IList<ParserInfo> ParsedInfos { get; init; }
    public required Dictionary<string, Person> DatabasePeople { get; init; }
    public bool ForceUpdate { get; init; } = false;
}

internal sealed record UpdateChapterComicInfoArgs
{
    public required MetadataSettingsDto Settings { get; init; }
    public required Chapter Chapter { get; init; }
    public required ComicInfo? ComicInfo { get; init; }
    public required Dictionary<string, Person> DatabasePeople { get; init; }
    public bool ForceUpdate { get; init; } = false;
}

/// <summary>
/// All code needed to Update a Series from a Scan action
/// </summary>
public class ProcessSeries(
    IUnitOfWork unitOfWork,
    ILogger<ProcessSeries> logger,
    IEventHub eventHub,
    IDirectoryService directoryService,
    ICacheHelper cacheHelper,
    IReadingItemService readingItemService,
    IFileService fileService,
    IReadingListService readingListService,
    IExternalMetadataService externalMetadataService)
    : IProcessSeries
{

    public async Task<int?> ProcessSeriesAsync(MetadataSettingsDto settings, IList<ParserInfo> parsedInfos, ProcessSeriesArgs args)
    {
        if (!parsedInfos.Any()) return null;

        var library = args.Library;

        var seriesAdded = false;
        var scanWatch = Stopwatch.StartNew();
        var seriesName = parsedInfos[0].Series;
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Updated, seriesName, args.LeftToProcess, args.TotalToProcess));
        logger.LogInformation("[ScannerService] Beginning series update on {SeriesName}, Forced: {ForceUpdate}", seriesName, args.ForceUpdate);

        // Check if there is a Series
        var firstInfo = parsedInfos[0];
        Series? series;
        try
        {
            // There is an opportunity to allow duplicate series here. Like if One is in root/marvel/batman and another is root/dc/batman
            // by changing to a ToList() and if multiple, doing a firstInfo.FirstFolder/RootFolder type check
            series =
                await unitOfWork.SeriesRepository.GetFullSeriesByAnyName(firstInfo.Series, firstInfo.LocalizedSeries,
                    library.Id, firstInfo.Format);
        }
        catch (Exception ex)
        {
            await ReportDuplicateSeriesLookup(library, firstInfo, ex);
            return null;
        }

        if (series == null)
        {
            seriesAdded = true;
            series = new SeriesBuilder(firstInfo.Series)
                .WithLocalizedName(firstInfo.LocalizedSeries)
                .Build();
            unitOfWork.SeriesRepository.Add(series);
        }

        if (series.LibraryId == 0) series.LibraryId = library.Id;

        try
        {
            logger.LogInformation("[ScannerService] Processing series {SeriesName} with {Count} files", series.OriginalName, parsedInfos.Count);

            // parsedInfos[0] is not the first volume or chapter. We need to find it using a ComicInfo check (as it uses firstParsedInfo for series sort)
            var firstParsedInfo = parsedInfos.FirstOrDefault(p => p.ComicInfo != null, firstInfo);
            var databasePeople = await LoadAndCreateMissingChapterPeople(parsedInfos);

            await UpdateVolumes(databasePeople, settings, series, parsedInfos, args.ForceUpdate);
            series.Pages = series.Volumes.Sum(v => v.Pages);

            series.NormalizedName = series.Name.ToNormalized();
            series.OriginalName ??= firstParsedInfo.Series;
            if (series.Format == MangaFormat.Unknown)
            {
                series.Format = firstParsedInfo.Format;
            }

            var removePrefix = library.RemovePrefixForSortName;
            var sortName = removePrefix ? BookSortTitlePrefixHelper.GetSortTitle(series.Name) : series.Name;

            if (string.IsNullOrEmpty(series.SortName))
            {
                series.SortName = sortName;
            }

            if (!series.SortNameLocked)
            {
                series.SortName = sortName;
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

            await UpdateSeriesMetadata(databasePeople, settings, series, library);

            // Update series FolderPath here
            await UpdateSeriesFolderPath(parsedInfos, library, series);

            series.UpdateLastFolderScanned();

            if (unitOfWork.HasChanges())
            {
                try
                {
                    await unitOfWork.CommitAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    logger.LogCritical(ex,
                        "[ScannerService] There was an issue writing to the database for series {SeriesName}",
                        series.Name);
                    await eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent($"There was an issue writing to the DB for Series {series.OriginalName}",
                            ex.Message));
                    return null;
                }
                catch (Exception ex)
                {
                    await unitOfWork.RollbackAsync();
                    logger.LogCritical(ex,
                        "[ScannerService] There was an issue writing to the database for series {SeriesName}",
                        series.Name);

                    await eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent($"There was an issue writing to the DB for Series {series.OriginalName}",
                            ex.Message));
                    return null;
                }


                // Process reading list after commit as we need to commit per list
                if (library.ManageReadingLists)
                {
                    await readingListService.CreateReadingListsFromSeries(series, library);
                }


                if (seriesAdded)
                {
                    // See if any recommendations can link up to the series and pre-fetch external metadata for the series
                    // BackgroundJob.Enqueue(() =>
                    //     _externalMetadataService.FetchSeriesMetadata(series.Id, series.Library.Type));

                    await eventHub.SendMessageAsync(MessageFactory.SeriesAdded,
                        MessageFactory.SeriesAddedEvent(series.Id, series.Name, series.LibraryId), false);
                }
                else
                {
                    await unitOfWork.ExternalSeriesMetadataRepository.LinkRecommendationsToSeries(series);
                }

                logger.LogInformation("[ScannerService] Finished series update on {SeriesName} in {Milliseconds} ms", seriesName, scanWatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ScannerService] There was an exception updating series for {SeriesName}", series.Name);
            return null;
        }

        if (seriesAdded)
        {
            await externalMetadataService.FetchSeriesMetadata(series.Id, series.Library.Type);
        }

        return series.Id;
    }

    private async Task ReportDuplicateSeriesLookup(Library library, ParserInfo firstInfo, Exception ex)
    {
        var seriesCollisions = await unitOfWork.SeriesRepository.GetAllSeriesByAnyName(firstInfo.LocalizedSeries, string.Empty, library.Id, firstInfo.Format);

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

            logger.LogError(ex, "[ScannerService] Scanner found a Series {SeriesName} which matched another Series {LocalizedName} in a different folder parallel to Library {LibraryName} root folder. This is not allowed. Please correct, scan will abort",
                firstInfo.Series, firstInfo.LocalizedSeries, library.Name);

            await eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent($"Library {library.Name} Series collision on {firstInfo.Series}",
                    htmlTable));
        }
    }


    private async Task UpdateSeriesFolderPath(IEnumerable<ParserInfo> parsedInfos, Library library, Series series)
    {
        var libraryFolders = library.Folders.Select(l => Parser.Parser.NormalizePath(l.Path)).ToList();
        var seriesFiles = parsedInfos.Select(f => Parser.Parser.NormalizePath(f.FullFilePath)).ToList();
        var seriesDirs = directoryService.FindHighestDirectoriesFromFiles(libraryFolders, seriesFiles);
        if (seriesDirs.Keys.Count == 0)
        {
            logger.LogCritical(
                "Scan Series has files spread outside a main series folder. This has negative performance effects. Please ensure all series are under a single folder from library");
            await eventHub.SendMessageAsync(MessageFactory.Info,
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
                logger.LogDebug("Updating {Series} FolderPath to {FolderPath}", series.Name, series.FolderPath);
            }
        }

        var lowestFolder = directoryService.FindLowestDirectoriesFromFiles(libraryFolders, seriesFiles);
        if (!string.IsNullOrEmpty(lowestFolder))
        {
            series.LowestFolderPath = lowestFolder;
            logger.LogDebug("Updating {Series} LowestFolderPath to {FolderPath}", series.Name, series.LowestFolderPath);
        }
    }


    private async Task UpdateSeriesMetadata(Dictionary<string, Person> databasePeople, MetadataSettingsDto settings, Series series, Library library)
    {
        series.Metadata ??= new SeriesMetadataBuilder().Build();
        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);

        var firstFile = firstChapter?.Files.FirstOrDefault();
        if (firstFile == null) return;

        var chapters = series.Volumes
            .SelectMany(volume => volume.Chapters)
            .ToList();

        // Update Metadata based on Chapter metadata
        if (!series.Metadata.ReleaseYearLocked)
        {
            series.Metadata.ReleaseYear = chapters.MinimumReleaseYear();
        }

        // Set the AgeRating as highest in all the comicInfos
        if (!series.Metadata.AgeRatingLocked)
        {
            series.Metadata.AgeRating = chapters.Max(chapter => chapter.AgeRating);

            if (settings.EnableExtendedMetadataProcessing)
            {
                var allTags = series.Metadata.Tags.Select(t => t.Title).Concat(series.Metadata.Genres.Select(g => g.Title));
                var updatedRating = ExternalMetadataService.DetermineAgeRating(allTags, settings.AgeRatingMappings);
                if (updatedRating > series.Metadata.AgeRating)
                {
                    series.Metadata.AgeRating = updatedRating;
                }
            }

        }

        DeterminePublicationStatus(series, chapters);

        if (!string.IsNullOrEmpty(firstChapter?.Summary) && !series.Metadata.SummaryLocked)
        {
            series.Metadata.Summary = firstChapter.Summary;
        }

        if (!series.Metadata.LanguageLocked)
        {
            if (!string.IsNullOrEmpty(firstChapter?.Language))
            {
                series.Metadata.Language = firstChapter.Language;
            }
            else if (!string.IsNullOrEmpty(library.DefaultLanguage))
            {
                series.Metadata.Language = library.DefaultLanguage;
            }
        }

        if (!string.IsNullOrEmpty(firstChapter?.WebLinks) && library.InheritWebLinksFromFirstChapter)
        {
            series.Metadata.WebLinks = firstChapter.WebLinks;
        }

        if (!string.IsNullOrEmpty(firstChapter?.SeriesGroup) && library.ManageCollections)
        {
            await UpdateCollectionTags(series, firstChapter);
        }

        #region PeopleAndTagsAndGenres

        foreach (var personRole in Enum.GetValues<PersonRole>().Where(r => r != PersonRole.Other))
        {
            var chapterPeople = chapters
                .SelectMany(c => c.People.Where(p => p.Role == personRole)).ToList();

            if (!ShouldUpdatePeopleForRole(series, chapterPeople, personRole)) continue;

            PersonHelper.UpdateSeriesMetadataPeople(databasePeople, series.Metadata, chapterPeople, personRole);
        }

        if (!series.Metadata.TagsLocked)
        {
            var tags = chapters.SelectMany(c => c.Tags).ToList();
            UpdateSeriesMetadataTags(series.Metadata.Tags, tags);
        }

        if (!series.Metadata.GenresLocked)
        {
            var genres = chapters.SelectMany(c => c.Genres).ToList();
            UpdateSeriesMetadataGenres(series.Metadata.Genres, genres);
        }

        #endregion

    }

    /// <summary>
    /// Ensure that we don't overwrite Person metadata when all metadata is coming from Kavita+ metadata match functionality
    /// </summary>
    /// <param name="series"></param>
    /// <param name="chapterPeople"></param>
    /// <param name="role"></param>
    /// <returns></returns>
    private static bool ShouldUpdatePeopleForRole(Series series, List<ChapterPeople> chapterPeople, PersonRole role)
    {
        if (chapterPeople.Count == 0) return false;

        // If metadata already has this role, but all entries are from KavitaPlus, we should retain them
        if (series.Metadata.AnyOfRole(role))
        {
            var existingPeople = series.Metadata.People.Where(p => p.Role == role);

            // If all existing people are KavitaPlus but new chapter people exist, we should still update
            if (existingPeople.All(p => p.KavitaPlusConnection))
            {
                return false; // Ensure we don't remove KavitaPlus people
            }

            return true; // Default case: metadata exists, and it's okay to update
        }

        return true;
    }

    private async Task UpdateCollectionTags(Series series, Chapter firstChapter)
    {
        // Get the default admin to associate these tags to
        var defaultAdmin = await unitOfWork.UserRepository.GetDefaultAdminUser(AppUserIncludes.Collections);
        if (defaultAdmin == null) return;

        logger.LogInformation("Collection tag(s) found for {SeriesName}, updating collections", series.Name);
        var sw = Stopwatch.StartNew();

        foreach (var collection in firstChapter.SeriesGroup.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            // Try to find an existing collection tag by its normalized name
            var normalizedCollectionName = collection.ToNormalized();
            var collectionTag = defaultAdmin.Collections.FirstOrDefault(c => c.NormalizedTitle == normalizedCollectionName);

            // If the collection tag does not exist, create a new one
            if (collectionTag == null)
            {
                logger.LogDebug("Creating new collection tag for {Tag}", collection);

                collectionTag = new AppUserCollectionBuilder(collection).Build();
                defaultAdmin.Collections.Add(collectionTag);

                unitOfWork.UserRepository.Update(defaultAdmin);

                await unitOfWork.CommitAsync();
            }

            // Check if the Series is already associated with this collection
            if (collectionTag.Items.Any(s => s.MatchesSeriesByName(series.NormalizedName, series.NormalizedLocalizedName)))
            {
                continue;
            }

            // Add the series to the collection tag
            collectionTag.Items.Add(series);

            // Update the collection age rating
            await unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(collectionTag);
        }

        logger.LogTrace("[TIME] Kavita took {Time} ms to process collections on Series: {Name}", sw.ElapsedMilliseconds, series.Name);
    }


    private static void UpdateSeriesMetadataTags(ICollection<Tag> metadataTags, IList<Tag> chapterTags)
    {
        // Create a HashSet of normalized titles for faster lookups
        var chapterTagTitles = new HashSet<string>(chapterTags.Select(t => t.NormalizedTitle));

        // Remove any tags from metadataTags that are not part of chapterTags
        var tagsToRemove = metadataTags
            .Where(mt => !chapterTagTitles.Contains(mt.NormalizedTitle))
            .ToList();

        if (tagsToRemove.Count > 0)
        {
            foreach (var tagToRemove in tagsToRemove)
            {
                metadataTags.Remove(tagToRemove);
            }
        }

        // Create a HashSet of metadataTags normalized titles for faster lookup
        var metadataTagTitles = new HashSet<string>(metadataTags.Select(mt => mt.NormalizedTitle));

        // Add any tags from chapterTags that do not already exist in metadataTags
        foreach (var tag in chapterTags)
        {
            if (!metadataTagTitles.Contains(tag.NormalizedTitle))
            {
                metadataTags.Add(tag);
            }
        }
    }

    private static void UpdateSeriesMetadataGenres(ICollection<Genre> metadataGenres, IList<Genre> chapterGenres)
    {
        // Create a HashSet of normalized titles for chapterGenres for fast lookup
        var chapterGenreTitles = new HashSet<string>(chapterGenres.Select(g => g.NormalizedTitle));

        // Remove any genres from metadataGenres that are not present in chapterGenres
        var genresToRemove = metadataGenres
            .Where(mg => !chapterGenreTitles.Contains(mg.NormalizedTitle))
            .ToList();

        foreach (var genreToRemove in genresToRemove)
        {
            metadataGenres.Remove(genreToRemove);
        }

        // Create a HashSet of metadataGenres normalized titles for fast lookup
        var metadataGenreTitles = new HashSet<string>(metadataGenres.Select(mg => mg.NormalizedTitle));

        // Add any genres from chapterGenres that are not already in metadataGenres
        foreach (var genre in chapterGenres)
        {
            if (!metadataGenreTitles.Contains(genre.NormalizedTitle))
            {
                metadataGenres.Add(genre);
            }
        }
    }



    private async Task UpdateSeriesMetadataPeople(SeriesMetadata metadata, ICollection<SeriesMetadataPeople> metadataPeople,
        IEnumerable<ChapterPeople> chapterPeople, PersonRole role)
    {
        await PersonHelper.UpdateSeriesMetadataPeopleAsync(metadata, metadataPeople, chapterPeople, role, unitOfWork);
    }

    private void DeterminePublicationStatus(Series series, List<Chapter> chapters)
    {
        try
        {
            // Count (aka expected total number of chapters or volumes from metadata) across all chapters
            series.Metadata.TotalCount = chapters.Max(chapter => chapter.TotalCount);
            // The actual number of count's defined across all chapter's metadata
            series.Metadata.MaxCount = chapters.Max(chapter => chapter.Count);

            var nonSpecialVolumes = series.Volumes
                .Where(v => v.MaxNumber.IsNot(Parser.Parser.SpecialVolumeNumber))
                .ToList();

            var maxVolume = (int)(nonSpecialVolumes.Any() ? nonSpecialVolumes.Max(v => v.MaxNumber) : 0);
            var maxChapter = (int)chapters.Max(c => c.MaxNumber);

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
            logger.LogCritical(ex, "There was an issue determining Publication Status");
            series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
        }
    }

    private async Task UpdateVolumes(Dictionary<string, Person> databasePeople, MetadataSettingsDto settings, Series series, IList<ParserInfo> parsedInfos, bool forceUpdate = false)
    {
        // Add new volumes and update chapters per volume
        var distinctVolumes = parsedInfos.DistinctVolumes();
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
                logger.LogCritical(ex, "[ScannerService] Kavita found corrupted volume entries on {SeriesName}. Please delete the series from Kavita via UI and rescan", series.Name);
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

            var infos = parsedInfos.Where(p => p.Volumes == volumeNumber).ToArray();

            await UpdateChapters(new UpdateChapterArgs
            {
                Settings = settings,
                Series = series,
                Volume = volume,
                ParsedInfos = infos,
                DatabasePeople = databasePeople,
                ForceUpdate = forceUpdate
            });
            volume.Pages = volume.Chapters.Sum(c => c.Pages);
        }

        // Remove existing volumes that aren't in parsedInfos
        RemoveVolumes(series, parsedInfos);
    }

    private void RemoveVolumes(Series series, IList<ParserInfo> parsedInfos)
    {

        var nonDeletedVolumes = series.Volumes
            .Where(v => parsedInfos.Select(p => p.Volumes).Contains(v.LookupName))
            .ToList();
        if (series.Volumes.Count == nonDeletedVolumes.Count) return;


        logger.LogDebug("[ScannerService] Removed {Count} volumes from {SeriesName} where parsed infos were not mapping with volume name",
            (series.Volumes.Count - nonDeletedVolumes.Count), series.Name);
        var deletedVolumes = series.Volumes.Except(nonDeletedVolumes);
        foreach (var volume in deletedVolumes)
        {
            var file = volume.Chapters.FirstOrDefault()?.Files?.FirstOrDefault()?.FilePath ?? string.Empty;
            if (!string.IsNullOrEmpty(file) && directoryService.FileSystem.File.Exists(file))
            {
                // This can happen when file is renamed and volume is removed
                logger.LogInformation(
                    "[ScannerService] Volume cleanup code was trying to remove a volume with a file still existing on disk (usually volume marker removed) File: {File}",
                    file);
            }

            logger.LogDebug("[ScannerService] Removed {SeriesName} - Volume {Volume}: {File}", series.Name, volume.Name, file);
        }

        series.Volumes = nonDeletedVolumes;
    }

    private async Task UpdateChapters(UpdateChapterArgs args)
    {
        // Add new chapters
        foreach (var info in args.ParsedInfos)
        {
            // Specials go into their own chapters with Range being their filename and IsSpecial = True. Non-Specials with Vol and Chap as 0
            // also are treated like specials for UI grouping.
            Chapter? chapter;
            try
            {
                chapter = args.Volume.Chapters.GetChapterByRange(info);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{FileName} mapped as '{Series} - Vol {Volume} Ch {Chapter}' is a duplicate, skipping", info.FullFilePath, info.Series, info.Volumes, info.Chapters);
                continue;
            }

            if (chapter == null)
            {
                logger.LogDebug(
                    "[ScannerService] Adding new chapter, {Series} - Vol {Volume} Ch {Chapter}", info.Series, info.Volumes, info.Chapters);
                chapter = ChapterBuilder.FromParserInfo(info).Build();
                args.Volume.Chapters.Add(chapter);
                args.Series.UpdateLastChapterAdded();
            }
            else
            {
                chapter.UpdateFrom(info);
            }


            // Add files
            AddOrUpdateFileForChapter(chapter, info, args.ForceUpdate);

            chapter.Number = Parser.Parser.MinNumberFromRange(info.Chapters).ToString(CultureInfo.InvariantCulture);
            chapter.MinNumber = Parser.Parser.MinNumberFromRange(info.Chapters);
            chapter.MaxNumber = Parser.Parser.MaxNumberFromRange(info.Chapters);
            chapter.Range = chapter.GetNumberTitle();

            if (!chapter.SortOrderLocked)
            {
                chapter.SortOrder = info.IssueOrder;
            }

            if (float.TryParse(chapter.Title, CultureInfo.InvariantCulture, out _))
            {
                // If we have float based chapters, first scan can have the chapter formatted as Chapter 0.2 - .2 as the title is wrong.
                chapter.Title = chapter.GetNumberTitle();
            }

            try
            {
                await UpdateChapterFromComicInfo(new UpdateChapterComicInfoArgs
                {
                    Settings = args.Settings,
                    Chapter = chapter,
                    ComicInfo = info.ComicInfo,
                    DatabasePeople = args.DatabasePeople,
                    ForceUpdate = args.ForceUpdate,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "There was some issue when updating chapter's metadata");
            }

        }

        RemoveChapters(args.Volume, args.ParsedInfos);
    }

    private void RemoveChapters(Volume volume, IList<ParserInfo> parsedInfos)
    {
        // Chapters to remove after enumeration
        var chaptersToRemove = new List<Chapter>();

        var existingChapters = volume.Chapters;

        // Extract the directories (without filenames) from parserInfos
        var parsedDirectories = parsedInfos
            .Select(p => Path.GetDirectoryName(p.FullFilePath))
            .Distinct()
            .ToList();

        foreach (var existingChapter in existingChapters)
        {
            var chapterFileDirectories = existingChapter.Files
                .Select(f => Path.GetDirectoryName(f.FilePath))
                .Distinct()
                .ToList();

            var hasMatchingDirectory = chapterFileDirectories.Exists(dir => parsedDirectories.Contains(dir));

            if (hasMatchingDirectory)
            {
                existingChapter.Files = existingChapter.Files
                    .Where(f => parsedInfos.Any(p => Parser.Parser.NormalizePath(p.FullFilePath) == Parser.Parser.NormalizePath(f.FilePath)))
                    .OrderByNatural(f => f.FilePath)
                    .ToList();

                existingChapter.Pages = existingChapter.Files.Sum(f => f.Pages);

                if (existingChapter.Files.Count != 0) continue;

                logger.LogDebug("[ScannerService] Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName}",
                    existingChapter.Range, volume.Name, parsedInfos[0].Series);
                chaptersToRemove.Add(existingChapter); // Mark chapter for removal
            }
            else
            {
                var filesExist = existingChapter.Files.Any(f => File.Exists(f.FilePath));
                if (filesExist) continue;

                logger.LogDebug("[ScannerService] Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName} as no files exist",
                    existingChapter.Range, volume.Name, parsedInfos[0].Series);
                chaptersToRemove.Add(existingChapter); // Mark chapter for removal
            }
        }

        // Remove chapters after the loop to avoid modifying the collection during enumeration
        foreach (var chapter in chaptersToRemove)
        {
            volume.Chapters.Remove(chapter);
        }
    }

    private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info, bool forceUpdate = false)
    {
        chapter.Files ??= [];
        var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
        var fileInfo = directoryService.FileSystem.FileInfo.New(info.FullFilePath);
        if (existingFile != null)
        {
            // TODO: I wonder if we can simplify this force check.
            existingFile.Format = info.Format;

            if (!forceUpdate && !fileService.HasFileBeenModifiedSince(existingFile.FilePath, existingFile.LastModified) && existingFile.Pages != 0) return;

            existingFile.Pages = readingItemService.GetNumberOfPages(info.FullFilePath, info.Format);
            existingFile.Extension = fileInfo.Extension.ToLowerInvariant();
            existingFile.FileName = Parser.Parser.RemoveExtensionIfSupported(existingFile.FilePath);
            existingFile.FilePath = Parser.Parser.NormalizePath(existingFile.FilePath);
            existingFile.Bytes = fileInfo.Length;
            existingFile.KoreaderHash = KoreaderHelper.HashContents(existingFile.FilePath);

            // We skip updating DB here with last modified time so that metadata refresh can do it
        }
        else
        {

            var file = new MangaFileBuilder(info.FullFilePath, info.Format, readingItemService.GetNumberOfPages(info.FullFilePath, info.Format))
                .WithExtension(fileInfo.Extension)
                .WithBytes(fileInfo.Length)
                .WithHash()
                .Build();
            chapter.Files.Add(file);
        }
    }

    private async Task UpdateChapterFromComicInfo(UpdateChapterComicInfoArgs args)
    {
        var comicInfo = args.ComicInfo;
        var chapter = args.Chapter;

        if (comicInfo == null) return;
        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null ||
            cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, args.ForceUpdate, firstFile)) return;

        var sw = Stopwatch.StartNew();
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

            // TODO: For each weblink, try to parse out some MetadataIds and store in the Chapter directly for matching (CBL)
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

        foreach (var personRole in Enum.GetValues<PersonRole>().Where(r => r != PersonRole.Other))
        {
            if (chapter.IsPersonRoleLocked(personRole)) continue;

            var comicInfoPeople = comicInfo.GetPeopleForRole(personRole);
            PersonHelper.UpdateChapterPeople(args.DatabasePeople, chapter, comicInfoPeople, personRole);
        }

        if (!chapter.GenresLocked || !chapter.TagsLocked)
        {
            var genres = TagHelper.GetTagValues(comicInfo.Genre);
            var tags = TagHelper.GetTagValues(comicInfo.Tags);

            ExternalMetadataService.GenerateExternalGenreAndTagsList(genres, tags, args.Settings,
                out var finalTags, out var finalGenres);

            if (!chapter.GenresLocked)
            {
                await UpdateChapterGenres(chapter, finalGenres);
            }

            if (!chapter.TagsLocked)
            {
                await UpdateChapterTags(chapter, finalTags);
            }


        }

        logger.LogTrace("[TIME] Kavita took {Time} ms to create/update Chapter: {File}", sw.ElapsedMilliseconds, chapter.Files.First().FileName);
    }

    private async Task UpdateChapterGenres(Chapter chapter, IEnumerable<string> genreNames)
    {
        try
        {
            await GenreHelper.UpdateChapterGenres(chapter, genreNames, unitOfWork);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error updating the chapter genres");
        }
    }

    private async Task UpdateChapterTags(Chapter chapter, IEnumerable<string> tagNames)
    {
        try
        {
            await TagHelper.UpdateChapterTags(chapter, tagNames, unitOfWork);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error updating the chapter tags");
        }
    }

    private async Task<Dictionary<string, Person>> LoadAndCreateMissingChapterPeople(IList<ParserInfo> parserInfos)
    {
        var comicInfos = parserInfos.Select(pi => pi.ComicInfo).WhereNotNull().ToList();

        var allPeople = Enum.GetValues<PersonRole>()
            .SelectMany(role => comicInfos.SelectMany(ci => ci.GetPeopleForRole(role)))
            .Select(person => new {Name = person, NormalizaedName = person.ToNormalized()})
            .DistinctBy(person => person.NormalizaedName)
            .ToList();

        var normalizedNames = allPeople.Select(p => p.NormalizaedName).ToList();

        var peopleInDatabase = await unitOfWork.PersonRepository.GetPeopleByNames(normalizedNames);
        var existingPeopleDict = PersonHelper.ConstructNameAndAliasDictionary(peopleInDatabase);

        var peopleToAdd = allPeople
            .Where(p => !existingPeopleDict.ContainsKey(p.NormalizaedName))
            .Select(p => new PersonBuilder(p.Name).Build())
            .ToList();

        await unitOfWork.DataContext.Person.AddRangeAsync(peopleToAdd);

        peopleToAdd.ForEach(p => existingPeopleDict[p.NormalizedName] = p);

        return existingPeopleDict;
    }
}
