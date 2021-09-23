using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using API.Parser;
using API.Services.Tasks.Scanner;
using API.SignalR;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class ScannerService : IScannerService
    {
       private readonly IUnitOfWork _unitOfWork;
       private readonly ILogger<ScannerService> _logger;
       private readonly IArchiveService _archiveService;
       private readonly IMetadataService _metadataService;
       private readonly IBookService _bookService;
       private readonly ICacheService _cacheService;
       private readonly IHubContext<MessageHub> _messageHub;
       private readonly NaturalSortComparer _naturalSort = new ();

       public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger, IArchiveService archiveService,
          IMetadataService metadataService, IBookService bookService, ICacheService cacheService, IHubContext<MessageHub> messageHub)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
          _metadataService = metadataService;
          _bookService = bookService;
          _cacheService = cacheService;
          _messageHub = messageHub;
       }

       [DisableConcurrentExecution(timeoutInSeconds: 360)]
       [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public async Task ScanSeries(int libraryId, int seriesId, bool forceUpdate, CancellationToken token)
       {
           var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
           var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
           var library = await _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId, seriesId);
           var dirs = DirectoryService.FindHighestDirectoriesFromFiles(library.Folders.Select(f => f.Path), files.Select(f => f.FilePath).ToList());
           var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new []{ seriesId });

           _logger.LogInformation("Beginning file scan on {SeriesName}", series.Name);
           var scanner = new ParseScannedFiles(_bookService, _logger);
           var parsedSeries = scanner.ScanLibrariesForSeries(library.Type, dirs.Keys, out var totalFiles, out var scanElapsedTime);

           // If a root level folder scan occurs, then multiple series gets passed in and thus we get a unique constraint issue
           // Hence we clear out anything but what we selected for
           var firstSeries = library.Series.FirstOrDefault();
           var keys = parsedSeries.Keys;
           foreach (var key in keys.Where(key => !firstSeries.NameInParserInfo(parsedSeries[key].FirstOrDefault()) || firstSeries?.Format != key.Format))
           {
               parsedSeries.Remove(key);
           }

           if (parsedSeries.Count == 0)
           {
               // We need to do an additional check for an edge case: If the scan ran and the files do not match the existing Series name, then it is very likely,
               // the files have crap naming and if we don't correct, the series will get deleted due to the parser not being able to fallback onto folder parsing as the root
               // is the series folder.
               var existingFolder = dirs.Keys.FirstOrDefault(key => key.Contains(series.OriginalName));
               if (dirs.Keys.Count == 1 && !string.IsNullOrEmpty(existingFolder))
               {
                   dirs = new Dictionary<string, string>();
                   var path = Path.GetPathRoot(existingFolder);
                   if (!string.IsNullOrEmpty(path))
                   {
                       dirs[path] = string.Empty;
                   }
               }
               _logger.LogDebug("{SeriesName} has bad naming convention, forcing rescan at a higher directory.", series.OriginalName);
               scanner = new ParseScannedFiles(_bookService, _logger);
               parsedSeries = scanner.ScanLibrariesForSeries(library.Type, dirs.Keys, out var totalFiles2, out var scanElapsedTime2);
               totalFiles += totalFiles2;
               scanElapsedTime += scanElapsedTime2;

               // If a root level folder scan occurs, then multiple series gets passed in and thus we get a unique constraint issue
               // Hence we clear out anything but what we selected for
               firstSeries = library.Series.FirstOrDefault();
               keys = parsedSeries.Keys;
               foreach (var key in keys.Where(key => !firstSeries.NameInParserInfo(parsedSeries[key].FirstOrDefault()) || firstSeries?.Format != key.Format))
               {
                   parsedSeries.Remove(key);
               }
           }

           var sw = new Stopwatch();
           UpdateLibrary(library, parsedSeries);

            _unitOfWork.LibraryRepository.Update(library);
            if (await _unitOfWork.CommitAsync())
            {
                _logger.LogInformation(
                    "Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {SeriesName}",
                    totalFiles, parsedSeries.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, series.Name);

                await CleanupDbEntities();
                BackgroundJob.Enqueue(() => _metadataService.RefreshMetadataForSeries(libraryId, seriesId, forceUpdate));
                BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
                // Tell UI that this series is done
                await _messageHub.Clients.All.SendAsync(SignalREvents.ScanSeries, MessageFactory.ScanSeriesEvent(seriesId), cancellationToken: token);
            }
            else
            {
                _logger.LogCritical(
                    "There was a critical error that resulted in a failed scan. Please check logs and rescan");
                await _unitOfWork.RollbackAsync();
            }

       }


       [DisableConcurrentExecution(timeoutInSeconds: 360)]
       [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public async Task ScanLibraries()
       {
           _logger.LogInformation("Starting Scan of All Libraries");
          var libraries = await _unitOfWork.LibraryRepository.GetLibrariesAsync();
          foreach (var lib in libraries)
          {
             await ScanLibrary(lib.Id, false);
          }
          _logger.LogInformation("Scan of All Libraries Finished");
       }


       /// <summary>
       /// Scans a library for file changes.
       /// Will kick off a scheduled background task to refresh metadata,
       /// ie) all entities will be rechecked for new cover images and comicInfo.xml changes
       /// </summary>
       /// <param name="libraryId"></param>
       /// <param name="forceUpdate"></param>
       [DisableConcurrentExecution(360)]
       [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public async Task ScanLibrary(int libraryId, bool forceUpdate)
       {
           Library library;
           try
           {
               library = await _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId);
           }
           catch (Exception ex)
           {
               // This usually only fails if user is not authenticated.
               _logger.LogError(ex, "There was an issue fetching Library {LibraryId}", libraryId);
               return;
           }

           _logger.LogInformation("Beginning file scan on {LibraryName}", library.Name);
           var scanner = new ParseScannedFiles(_bookService, _logger);
           var series = scanner.ScanLibrariesForSeries(library.Type, library.Folders.Select(fp => fp.Path), out var totalFiles, out var scanElapsedTime);
           scanner = null;

           foreach (var folderPath in library.Folders)
           {
               folderPath.LastScanned = DateTime.Now;
           }
           var sw = Stopwatch.StartNew();

           UpdateLibrary(library, series);

           _unitOfWork.LibraryRepository.Update(library);
           if (await _unitOfWork.CommitAsync())
           {
               _logger.LogInformation(
                   "Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                   totalFiles, series.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, library.Name);
           }
           else
           {
               _logger.LogCritical(
                   "There was a critical error that resulted in a failed scan. Please check logs and rescan");
           }

           await CleanupAbandonedChapters();

           BackgroundJob.Enqueue(() => _metadataService.RefreshMetadata(libraryId, forceUpdate));
           await _messageHub.Clients.All.SendAsync(SignalREvents.ScanLibrary, MessageFactory.ScanLibraryEvent(libraryId, "complete"));
       }

       /// <summary>
       /// Remove any user progress rows that no longer exist since scan library ran and deleted series/volumes/chapters
       /// </summary>
       private async Task CleanupAbandonedChapters()
       {
          var cleanedUp = await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
          _logger.LogInformation("Removed {Count} abandoned progress rows", cleanedUp);
       }


       /// <summary>
       /// Cleans up any abandoned rows due to removals from Scan loop
       /// </summary>
       private async Task CleanupDbEntities()
       {
           await CleanupAbandonedChapters();
           var cleanedUp = await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
           _logger.LogInformation("Removed {Count} abandoned collection tags", cleanedUp);
       }

       private void UpdateLibrary(Library library, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
       {
          if (parsedSeries == null) throw new ArgumentNullException(nameof(parsedSeries));

          // First, remove any series that are not in parsedSeries list
          var missingSeries = FindSeriesNotOnDisk(library.Series, parsedSeries).ToList();
          library.Series = RemoveMissingSeries(library.Series, missingSeries, out var removeCount);
          if (removeCount > 0)
          {
             _logger.LogInformation("Removed {RemoveMissingSeries} series that are no longer on disk:", removeCount);
             foreach (var s in missingSeries)
             {
                _logger.LogDebug("Removed {SeriesName} ({Format})", s.Name, s.Format);
             }
          }

          // Add new series that have parsedInfos
          foreach (var (key, infos) in parsedSeries)
          {
              // Key is normalized already
             Series existingSeries;
             try
             {
                existingSeries = library.Series.SingleOrDefault(s =>
                    (s.NormalizedName == key.NormalizedName || Parser.Parser.Normalize(s.OriginalName) == key.NormalizedName)
                    && (s.Format == key.Format || s.Format == MangaFormat.Unknown));
             }
             catch (Exception e)
             {
                _logger.LogCritical(e, "There are multiple series that map to normalized key {Key}. You can manually delete the entity via UI and rescan to fix it", key.NormalizedName);
                var duplicateSeries = library.Series.Where(s => s.NormalizedName == key.NormalizedName || Parser.Parser.Normalize(s.OriginalName) == key.NormalizedName).ToList();
                foreach (var series in duplicateSeries)
                {
                   _logger.LogCritical("{Key} maps with {Series}", key.Name, series.OriginalName);
                }

                continue;
             }
             if (existingSeries == null)
             {
                existingSeries = DbFactory.Series(infos[0].Series);
                existingSeries.Format = key.Format;
                library.Series.Add(existingSeries);
             }

             existingSeries.NormalizedName = Parser.Parser.Normalize(existingSeries.Name);
             existingSeries.OriginalName ??= infos[0].Series;
             existingSeries.Metadata ??= DbFactory.SeriesMetadata(new List<CollectionTag>());
             existingSeries.Format = key.Format;
          }

          // Now, we only have to deal with series that exist on disk. Let's recalculate the volumes for each series
          var librarySeries = library.Series.ToList();
          Parallel.ForEach(librarySeries, (series) =>
          {
             try
             {
                _logger.LogInformation("Processing series {SeriesName}", series.OriginalName);
                UpdateVolumes(series, ParseScannedFiles.GetInfosByName(parsedSeries, series).ToArray());
                series.Pages = series.Volumes.Sum(v => v.Pages);
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, "There was an exception updating volumes for {SeriesName}", series.Name);
             }
          });

          // Last step, remove any series that have no pages
          library.Series = library.Series.Where(s => s.Pages > 0).ToList();
       }

       public IEnumerable<Series> FindSeriesNotOnDisk(ICollection<Series> existingSeries, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
       {
           var foundSeries = parsedSeries.Select(s => s.Key.Name).ToList();
           return existingSeries.Where(es => !es.NameInList(foundSeries) && !SeriesHasMatchingParserInfoFormat(es, parsedSeries));
       }

       /// <summary>
       /// Checks each parser info to see if there is a name match and if so, checks if the format matches the Series object.
       /// This accounts for if the Series has an Unknown type and if so, considers it matching.
       /// </summary>
       /// <param name="series"></param>
       /// <param name="parsedSeries"></param>
       /// <returns></returns>
       private static bool SeriesHasMatchingParserInfoFormat(Series series,
           Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
       {
           var format = MangaFormat.Unknown;
           foreach (var pSeries in parsedSeries.Keys)
           {
               var name = pSeries.Name;
               var normalizedName = Parser.Parser.Normalize(name);

               if (normalizedName == series.NormalizedName ||
                   normalizedName == Parser.Parser.Normalize(series.Name) ||
                   name == series.Name || name == series.LocalizedName ||
                   name == series.OriginalName ||
                   normalizedName == Parser.Parser.Normalize(series.OriginalName))
               {
                   format = pSeries.Format;
                   break;
               }
           }

           if (series.Format == MangaFormat.Unknown && format != MangaFormat.Unknown)
           {
               return true;
           }

           return format == series.Format;
       }

       /// <summary>
       /// Removes all instances of missingSeries' Series from existingSeries Collection. Existing series is updated by
       /// reference and the removed element count is returned.
       /// </summary>
       /// <param name="existingSeries">Existing Series in DB</param>
       /// <param name="missingSeries">Series not found on disk or can't be parsed</param>
       /// <param name="removeCount"></param>
       /// <returns>the updated existingSeries</returns>
       public static ICollection<Series> RemoveMissingSeries(ICollection<Series> existingSeries, IEnumerable<Series> missingSeries, out int removeCount)
       {
          var existingCount = existingSeries.Count;
          var missingList = missingSeries.ToList();

          existingSeries = existingSeries.Where(
             s => !missingList.Exists(
                m => m.NormalizedName.Equals(s.NormalizedName) && m.Format == s.Format)).ToList();

          removeCount = existingCount -  existingSeries.Count;

          return existingSeries;
       }

       private void UpdateVolumes(Series series, ParserInfo[] parsedInfos)
       {
          var startingVolumeCount = series.Volumes.Count;
          // Add new volumes and update chapters per volume
          var distinctVolumes = parsedInfos.DistinctVolumes();
          _logger.LogDebug("Updating {DistinctVolumes} volumes on {SeriesName}", distinctVolumes.Count, series.Name);
          foreach (var volumeNumber in distinctVolumes)
          {
             var volume = series.Volumes.SingleOrDefault(s => s.Name == volumeNumber);
             if (volume == null)
             {
                volume = DbFactory.Volume(volumeNumber);
                series.Volumes.Add(volume);
             }

             _logger.LogDebug("Parsing {SeriesName} - Volume {VolumeNumber}", series.Name, volume.Name);
             var infos = parsedInfos.Where(p => p.Volumes == volumeNumber).ToArray();
             UpdateChapters(volume, infos);
             volume.Pages = volume.Chapters.Sum(c => c.Pages);
          }

          // Remove existing volumes that aren't in parsedInfos
          var nonDeletedVolumes = series.Volumes.Where(v => parsedInfos.Select(p => p.Volumes).Contains(v.Name)).ToList();
          if (series.Volumes.Count != nonDeletedVolumes.Count)
          {
             _logger.LogDebug("Removed {Count} volumes from {SeriesName} where parsed infos were not mapping with volume name",
                (series.Volumes.Count - nonDeletedVolumes.Count), series.Name);
             var deletedVolumes = series.Volumes.Except(nonDeletedVolumes);
             foreach (var volume in deletedVolumes)
             {
                var file = volume.Chapters.FirstOrDefault()?.Files.FirstOrDefault()?.FilePath ?? "no files";
                if (new FileInfo(file).Exists)
                {
                   _logger.LogError("Volume cleanup code was trying to remove a volume with a file still existing on disk. File: {File}", file);
                }
                _logger.LogDebug("Removed {SeriesName} - Volume {Volume}: {File}", series.Name, volume.Name, file);
             }

             series.Volumes = nonDeletedVolumes;
          }

          _logger.LogDebug("Updated {SeriesName} volumes from {StartingVolumeCount} to {VolumeCount}",
             series.Name, startingVolumeCount, series.Volumes.Count);
       }

       /// <summary>
       ///
       /// </summary>
       /// <param name="volume"></param>
       /// <param name="parsedInfos"></param>
       private void UpdateChapters(Volume volume, ParserInfo[] parsedInfos)
       {
          // Add new chapters
          foreach (var info in parsedInfos)
          {
             // Specials go into their own chapters with Range being their filename and IsSpecial = True. Non-Specials with Vol and Chap as 0
             // also are treated like specials for UI grouping.
             Chapter chapter;
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
                   "Adding new chapter, {Series} - Vol {Volume} Ch {Chapter}", info.Series, info.Volumes, info.Chapters);
                volume.Chapters.Add(DbFactory.Chapter(info));
             }
             else
             {
                chapter.UpdateFrom(info);
             }

          }

          // Add files
          foreach (var info in parsedInfos)
          {
             var specialTreatment = info.IsSpecialInfo();
             Chapter chapter;
             try
             {
                chapter = volume.Chapters.GetChapterByRange(info);
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, "There was an exception parsing chapter. Skipping {SeriesName} Vol {VolumeNumber} Chapter {ChapterNumber} - Special treatment: {NeedsSpecialTreatment}", info.Series, volume.Name, info.Chapters, specialTreatment);
                continue;
             }
             if (chapter == null) continue;
             AddOrUpdateFileForChapter(chapter, info);
             chapter.Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + string.Empty;
             chapter.Range = specialTreatment ? info.Filename : info.Chapters;
          }


          // Remove chapters that aren't in parsedInfos or have no files linked
          var existingChapters = volume.Chapters.ToList();
          foreach (var existingChapter in existingChapters)
          {
             if (existingChapter.Files.Count == 0 || !parsedInfos.HasInfo(existingChapter))
             {
                _logger.LogDebug("Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName}", existingChapter.Range, volume.Name, parsedInfos[0].Series);
                volume.Chapters.Remove(existingChapter);
             }
             else
             {
                // Ensure we remove any files that no longer exist AND order
                existingChapter.Files = existingChapter.Files
                   .Where(f => parsedInfos.Any(p => p.FullFilePath == f.FilePath))
                   .OrderBy(f => f.FilePath, _naturalSort).ToList();
                existingChapter.Pages = existingChapter.Files.Sum(f => f.Pages);
             }
          }
       }

       private MangaFile CreateMangaFile(ParserInfo info)
       {
          switch (info.Format)
          {
             case MangaFormat.Archive:
             {
                return new MangaFile()
                {
                   FilePath = info.FullFilePath,
                   Format = info.Format,
                   Pages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath)
                };
             }
             case MangaFormat.Pdf:
             case MangaFormat.Epub:
             {
                return new MangaFile()
                {
                   FilePath = info.FullFilePath,
                   Format = info.Format,
                   Pages = _bookService.GetNumberOfPages(info.FullFilePath)
                };
             }
             case MangaFormat.Image:
             {
               return new MangaFile()
               {
                 FilePath = info.FullFilePath,
                 Format = info.Format,
                 Pages = 1
               };
             }
             default:
                _logger.LogWarning("[Scanner] Ignoring {Filename}. File type is not supported", info.Filename);
                break;
          }

          return null;
       }

       private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info)
       {
          chapter.Files ??= new List<MangaFile>();
          var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
          if (existingFile != null)
          {
             existingFile.Format = info.Format;
             if (existingFile.HasFileBeenModified() || existingFile.Pages == 0)
             {
                existingFile.Pages = (existingFile.Format == MangaFormat.Epub || existingFile.Format == MangaFormat.Pdf)
                   ? _bookService.GetNumberOfPages(info.FullFilePath)
                   : _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath);
             }
          }
          else
          {
             var file = CreateMangaFile(info);
             if (file != null)
             {
                chapter.Files.Add(file);
             }
          }
       }
    }
}
