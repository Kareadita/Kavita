using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
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
       public async Task ScanSeries(int libraryId, int seriesId, CancellationToken token)
       {
           var sw = new Stopwatch();
           var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
           var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
           var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new[] {seriesId});
           var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
           var folderPaths = library.Folders.Select(f => f.Path).ToList();

           // Check if any of the folder roots are not available (ie disconnected from network, etc) and fail if any of them are
           if (folderPaths.Any(f => !DirectoryService.IsDriveMounted(f)))
           {
               _logger.LogError("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
               return;
           }

           var dirs = DirectoryService.FindHighestDirectoriesFromFiles(folderPaths, files.Select(f => f.FilePath).ToList());

           _logger.LogInformation("Beginning file scan on {SeriesName}", series.Name);
           var scanner = new ParseScannedFiles(_bookService, _logger);
           var parsedSeries = scanner.ScanLibrariesForSeries(library.Type, dirs.Keys, out var totalFiles, out var scanElapsedTime);

           // Remove any parsedSeries keys that don't belong to our series. This can occur when users store 2 series in the same folder
           RemoveParsedInfosNotForSeries(parsedSeries, series);

           // If nothing was found, first validate any of the files still exist. If they don't then we have a deletion and can skip the rest of the logic flow
           if (parsedSeries.Count == 0)
           {
               var anyFilesExist =
                   (await _unitOfWork.SeriesRepository.GetFilesForSeries(series.Id)).Any(m => File.Exists(m.FilePath));

               if (!anyFilesExist)
               {
                   try
                   {
                       _unitOfWork.SeriesRepository.Remove(series);
                       await CommitAndSend(totalFiles, parsedSeries, sw, scanElapsedTime, series);
                   }
                   catch (Exception ex)
                   {
                       _logger.LogCritical(ex, "There was an error during ScanSeries to delete the series");
                       await _unitOfWork.RollbackAsync();
                   }

               }
               else
               {
                   // We need to do an additional check for an edge case: If the scan ran and the files do not match the existing Series name, then it is very likely,
                   // the files have crap naming and if we don't correct, the series will get deleted due to the parser not being able to fallback onto folder parsing as the root
                   // is the series folder.
                   var existingFolder = dirs.Keys.FirstOrDefault(key => key.Contains(series.OriginalName));
                   if (dirs.Keys.Count == 1 && !string.IsNullOrEmpty(existingFolder))
                   {
                       dirs = new Dictionary<string, string>();
                       var path = Directory.GetParent(existingFolder)?.FullName;
                       if (!folderPaths.Contains(path) || !folderPaths.Any(p => p.Contains(path ?? string.Empty)))
                       {
                           _logger.LogInformation("[ScanService] Aborted: {SeriesName} has bad naming convention and sits at root of library. Cannot scan series without deletion occuring. Correct file names to have Series Name within it or perform Scan Library", series.OriginalName);
                           return;
                       }
                       if (!string.IsNullOrEmpty(path))
                       {
                           dirs[path] = string.Empty;
                       }
                   }

                   _logger.LogInformation("{SeriesName} has bad naming convention, forcing rescan at a higher directory", series.OriginalName);
                   scanner = new ParseScannedFiles(_bookService, _logger);
                   parsedSeries = scanner.ScanLibrariesForSeries(library.Type, dirs.Keys, out var totalFiles2, out var scanElapsedTime2);
                   totalFiles += totalFiles2;
                   scanElapsedTime += scanElapsedTime2;
                   RemoveParsedInfosNotForSeries(parsedSeries, series);
               }
           }

           // At this point, parsedSeries will have at least one key and we can perform the update. If it still doesn't, just return and don't do anything
           if (parsedSeries.Count == 0) return;

           try
           {
               UpdateSeries(series, parsedSeries);
               await CommitAndSend(totalFiles, parsedSeries, sw, scanElapsedTime, series);
           }
           catch (Exception ex)
           {
               _logger.LogCritical(ex, "There was an error during ScanSeries to update the series");
               await _unitOfWork.RollbackAsync();
           }
           // Tell UI that this series is done
           await _messageHub.Clients.All.SendAsync(SignalREvents.ScanSeries, MessageFactory.ScanSeriesEvent(seriesId, series.Name), token);
           await CleanupDbEntities();
           BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
           BackgroundJob.Enqueue(() => _metadataService.RefreshMetadataForSeries(libraryId, series.Id, false));
       }

       private static void RemoveParsedInfosNotForSeries(Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Series series)
       {
           var keys = parsedSeries.Keys;
           foreach (var key in keys.Where(key =>
               !series.NameInParserInfo(parsedSeries[key].FirstOrDefault()) || series.Format != key.Format))
           {
               parsedSeries.Remove(key);
           }
       }

       private async Task CommitAndSend(int totalFiles,
           Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Stopwatch sw, long scanElapsedTime, Series series)
       {
           if (_unitOfWork.HasChanges())
           {
               await _unitOfWork.CommitAsync();
               _logger.LogInformation(
                   "Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {SeriesName}",
                   totalFiles, parsedSeries.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, series.Name);
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
             await ScanLibrary(lib.Id);
          }
          _logger.LogInformation("Scan of All Libraries Finished");
       }


       /// <summary>
       /// Scans a library for file changes.
       /// Will kick off a scheduled background task to refresh metadata,
       /// ie) all entities will be rechecked for new cover images and comicInfo.xml changes
       /// </summary>
       /// <param name="libraryId"></param>
       [DisableConcurrentExecution(360)]
       [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public async Task ScanLibrary(int libraryId)
       {
           Library library;
           try
           {
               library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
           }
           catch (Exception ex)
           {
               // This usually only fails if user is not authenticated.
               _logger.LogError(ex, "[ScannerService] There was an issue fetching Library {LibraryId}", libraryId);
               return;
           }

           // Check if any of the folder roots are not available (ie disconnected from network, etc) and fail if any of them are
           if (library.Folders.Any(f => !DirectoryService.IsDriveMounted(f.Path)))
           {
               _logger.LogError("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
               return;
           }


           _logger.LogInformation("[ScannerService] Beginning file scan on {LibraryName}", library.Name);
           await _messageHub.Clients.All.SendAsync(SignalREvents.ScanLibraryProgress,
               MessageFactory.ScanLibraryProgressEvent(libraryId, 0));

           var scanner = new ParseScannedFiles(_bookService, _logger);
           var series = scanner.ScanLibrariesForSeries(library.Type, library.Folders.Select(fp => fp.Path), out var totalFiles, out var scanElapsedTime);

           foreach (var folderPath in library.Folders)
           {
               folderPath.LastScanned = DateTime.Now;
           }
           var sw = Stopwatch.StartNew();

           await UpdateLibrary(library, series);

           library.LastScanned = DateTime.Now;
           _unitOfWork.LibraryRepository.Update(library);
           if (await _unitOfWork.CommitAsync())
           {
               _logger.LogInformation(
                   "[ScannerService] Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                   totalFiles, series.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, library.Name);
           }
           else
           {
               _logger.LogCritical(
                   "[ScannerService] There was a critical error that resulted in a failed scan. Please check logs and rescan");
           }

           await CleanupDbEntities();

           BackgroundJob.Enqueue(() => _metadataService.RefreshMetadata(libraryId, false));
           await _messageHub.Clients.All.SendAsync(SignalREvents.ScanLibraryProgress,
               MessageFactory.ScanLibraryProgressEvent(libraryId, 1F));
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

       private async Task UpdateLibrary(Library library, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
       {
          if (parsedSeries == null) return;

          // Library contains no Series, so we need to fetch series in groups of ChunkSize
          var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
          var stopwatch = Stopwatch.StartNew();
          var totalTime = 0L;

          // Update existing series
          _logger.LogInformation("[ScannerService] Updating existing series for {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size",
              library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);
          for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
          {
              if (chunkInfo.TotalChunks == 0) continue;
              totalTime += stopwatch.ElapsedMilliseconds;
              stopwatch.Restart();
              _logger.LogInformation("[ScannerService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
                  chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);
              var nonLibrarySeries = await _unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id, new UserParams()
              {
                  PageNumber = chunk,
                  PageSize = chunkInfo.ChunkSize
              });

              // First, remove any series that are not in parsedSeries list
              var missingSeries = FindSeriesNotOnDisk(nonLibrarySeries, parsedSeries).ToList();

              foreach (var missing in missingSeries)
              {
                  _unitOfWork.SeriesRepository.Remove(missing);
              }

              var cleanedSeries = RemoveMissingSeries(nonLibrarySeries, missingSeries, out var removeCount);
              if (removeCount > 0)
              {
                  _logger.LogInformation("[ScannerService] Removed {RemoveMissingSeries} series that are no longer on disk:", removeCount);
                  foreach (var s in missingSeries)
                  {
                      _logger.LogDebug("[ScannerService] Removed {SeriesName} ({Format})", s.Name, s.Format);
                  }
              }

              // Now, we only have to deal with series that exist on disk. Let's recalculate the volumes for each series
              var librarySeries = cleanedSeries.ToList();
              Parallel.ForEach(librarySeries, (series) =>
              {
                  UpdateSeries(series, parsedSeries);
              });

              try
              {
                  await _unitOfWork.CommitAsync();
              }
              catch (Exception ex)
              {
                  _logger.LogCritical(ex, "[ScannerService] There was an issue writing to the DB. Chunk {ChunkNumber} did not save to DB. If debug mode, series to check will be printed", chunk);
                  foreach (var series in nonLibrarySeries)
                  {
                      _logger.LogDebug("[ScannerService] There may be a constraint issue with {SeriesName}", series.OriginalName);
                  }
                  await _messageHub.Clients.All.SendAsync(SignalREvents.ScanLibraryError,
                      MessageFactory.ScanLibraryError(library.Id));
                  continue;
              }
              _logger.LogInformation(
                  "[ScannerService] Processed {SeriesStart} - {SeriesEnd} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                  chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, totalTime, library.Name);

              // Emit any series removed
              foreach (var missing in missingSeries)
              {
                  await _messageHub.Clients.All.SendAsync(SignalREvents.SeriesRemoved, MessageFactory.SeriesRemovedEvent(missing.Id, missing.Name, library.Id));
              }

              var progress =  Math.Max(0, Math.Min(1, ((chunk + 1F) * chunkInfo.ChunkSize) / chunkInfo.TotalSize));
              await _messageHub.Clients.All.SendAsync(SignalREvents.ScanLibraryProgress,
                  MessageFactory.ScanLibraryProgressEvent(library.Id, progress));
          }


          // Add new series that have parsedInfos
          _logger.LogDebug("[ScannerService] Adding new series");
          var newSeries = new List<Series>();
          var allSeries = (await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(library.Id)).ToList();
          _logger.LogDebug("[ScannerService] Fetched {AllSeriesCount} series for comparing new series with. There should be {DeltaToParsedSeries} new series",
              allSeries.Count, parsedSeries.Count - allSeries.Count);
          foreach (var (key, infos) in parsedSeries)
          {
              // Key is normalized already
              Series existingSeries;
              try
              {
                  existingSeries = allSeries.SingleOrDefault(s => FindSeries(s, key));
              }
              catch (Exception e)
              {
                  // NOTE: If I ever want to put Duplicates table, this is where it can go
                  _logger.LogCritical(e, "[ScannerService] There are multiple series that map to normalized key {Key}. You can manually delete the entity via UI and rescan to fix it. This will be skipped", key.NormalizedName);
                  var duplicateSeries = allSeries.Where(s => FindSeries(s, key));
                  foreach (var series in duplicateSeries)
                  {
                      _logger.LogCritical("[ScannerService] Duplicate Series Found: {Key} maps with {Series}", key.Name, series.OriginalName);
                  }

                  continue;
              }

              if (existingSeries != null) continue;

              var s = DbFactory.Series(infos[0].Series);
              s.Format = key.Format;
              s.LibraryId = library.Id; // We have to manually set this since we aren't adding the series to the Library's series.
              newSeries.Add(s);
          }

          var i = 0;
          foreach(var series in newSeries)
          {
              _logger.LogDebug("[ScannerService] Processing series {SeriesName}", series.OriginalName);
              UpdateSeries(series, parsedSeries);
              _unitOfWork.SeriesRepository.Attach(series);
              try
              {
                  await _unitOfWork.CommitAsync();
                  _logger.LogInformation(
                      "[ScannerService] Added {NewSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                      newSeries.Count, stopwatch.ElapsedMilliseconds, library.Name);

                  // Inform UI of new series added
                  await _messageHub.Clients.All.SendAsync(SignalREvents.SeriesAdded, MessageFactory.SeriesAddedEvent(series.Id, series.Name, library.Id));
              }
              catch (Exception ex)
              {
                  _logger.LogCritical(ex, "[ScannerService] There was a critical exception adding new series entry for {SeriesName} with a duplicate index key: {IndexKey} ",
                      series.Name, $"{series.Name}_{series.NormalizedName}_{series.LocalizedName}_{series.LibraryId}_{series.Format}");
              }

              var progress =  Math.Max(0F, Math.Min(1F, i * 1F / newSeries.Count));
              await _messageHub.Clients.All.SendAsync(SignalREvents.ScanLibraryProgress,
                  MessageFactory.ScanLibraryProgressEvent(library.Id, progress));
              i++;
          }

          _logger.LogInformation(
              "[ScannerService] Added {NewSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
              newSeries.Count, stopwatch.ElapsedMilliseconds, library.Name);
       }

       private static bool FindSeries(Series series, ParsedSeries parsedInfoKey)
       {
           return (series.NormalizedName.Equals(parsedInfoKey.NormalizedName) || Parser.Parser.Normalize(series.OriginalName).Equals(parsedInfoKey.NormalizedName))
                  && (series.Format == parsedInfoKey.Format || series.Format == MangaFormat.Unknown);
       }

       private void UpdateSeries(Series series, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
       {
           try
           {
               _logger.LogInformation("[ScannerService] Processing series {SeriesName}", series.OriginalName);

               var parsedInfos = ParseScannedFiles.GetInfosByName(parsedSeries, series);
               UpdateVolumes(series, parsedInfos);
               series.Pages = series.Volumes.Sum(v => v.Pages);

               series.NormalizedName = Parser.Parser.Normalize(series.Name);
               series.Metadata ??= DbFactory.SeriesMetadata(new List<CollectionTag>());
               if (series.Format == MangaFormat.Unknown)
               {
                   series.Format = parsedInfos[0].Format;
               }
               series.OriginalName ??= parsedInfos[0].Series;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "[ScannerService] There was an exception updating volumes for {SeriesName}", series.Name);
           }
       }

       public static IEnumerable<Series> FindSeriesNotOnDisk(IEnumerable<Series> existingSeries, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
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
       public static IEnumerable<Series> RemoveMissingSeries(IList<Series> existingSeries, IEnumerable<Series> missingSeries, out int removeCount)
       {
          var existingCount = existingSeries.Count;
          var missingList = missingSeries.ToList();

          existingSeries = existingSeries.Where(
             s => !missingList.Exists(
                m => m.NormalizedName.Equals(s.NormalizedName) && m.Format == s.Format)).ToList();

          removeCount = existingCount -  existingSeries.Count;

          return existingSeries;
       }

       private void UpdateVolumes(Series series, IList<ParserInfo> parsedInfos)
       {
          var startingVolumeCount = series.Volumes.Count;
          // Add new volumes and update chapters per volume
          var distinctVolumes = parsedInfos.DistinctVolumes();
          _logger.LogDebug("[ScannerService] Updating {DistinctVolumes} volumes on {SeriesName}", distinctVolumes.Count, series.Name);
          foreach (var volumeNumber in distinctVolumes)
          {
             var volume = series.Volumes.SingleOrDefault(s => s.Name == volumeNumber);
             if (volume == null)
             {
                volume = DbFactory.Volume(volumeNumber);
                series.Volumes.Add(volume);
                _unitOfWork.VolumeRepository.Add(volume);
             }

             _logger.LogDebug("[ScannerService] Parsing {SeriesName} - Volume {VolumeNumber}", series.Name, volume.Name);
             var infos = parsedInfos.Where(p => p.Volumes == volumeNumber).ToArray();
             UpdateChapters(volume, infos);
             volume.Pages = volume.Chapters.Sum(c => c.Pages);
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
                 var file = volume.Chapters.FirstOrDefault()?.Files?.FirstOrDefault()?.FilePath ?? "";
                 if (!string.IsNullOrEmpty(file) && File.Exists(file))
                 {
                     _logger.LogError(
                         "[ScannerService] Volume cleanup code was trying to remove a volume with a file still existing on disk. File: {File}",
                         file);
                 }

                 _logger.LogDebug("[ScannerService] Removed {SeriesName} - Volume {Volume}: {File}", series.Name, volume.Name, file);
             }

             series.Volumes = nonDeletedVolumes;
          }

          _logger.LogDebug("[ScannerService] Updated {SeriesName} volumes from {StartingVolumeCount} to {VolumeCount}",
             series.Name, startingVolumeCount, series.Volumes.Count);
       }

       /// <summary>
       ///
       /// </summary>
       /// <param name="volume"></param>
       /// <param name="parsedInfos"></param>
       private void UpdateChapters(Volume volume, IList<ParserInfo> parsedInfos)
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
                   "[ScannerService] Adding new chapter, {Series} - Vol {Volume} Ch {Chapter}", info.Series, info.Volumes, info.Chapters);
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
                _logger.LogDebug("[ScannerService] Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName}", existingChapter.Range, volume.Name, parsedInfos[0].Series);
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
           MangaFile mangaFile = null;
           switch (info.Format)
          {
             case MangaFormat.Archive:
             {
                 mangaFile = new MangaFile()
                {
                   FilePath = info.FullFilePath,
                   Format = info.Format,
                   Pages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath)
                };
                 break;
             }
             case MangaFormat.Pdf:
             case MangaFormat.Epub:
             {
                 mangaFile = new MangaFile()
                {
                   FilePath = info.FullFilePath,
                   Format = info.Format,
                   Pages = _bookService.GetNumberOfPages(info.FullFilePath)
                };
                 break;
             }
             case MangaFormat.Image:
             {
                 mangaFile = new MangaFile()
                 {
                     FilePath = info.FullFilePath,
                     Format = info.Format,
                     Pages = 1
                 };
                 break;
             }
             default:
                _logger.LogWarning("[Scanner] Ignoring {Filename}. File type is not supported", info.Filename);
                break;
          }

           mangaFile?.UpdateLastModified();
           return mangaFile;
       }

       private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info)
       {
          chapter.Files ??= new List<MangaFile>();
          var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
          if (existingFile != null)
          {
             existingFile.Format = info.Format;
             if (!existingFile.HasFileBeenModified() && existingFile.Pages != 0) return;
             switch (existingFile.Format)
             {
                 case MangaFormat.Epub:
                 case MangaFormat.Pdf:
                     existingFile.Pages = _bookService.GetNumberOfPages(info.FullFilePath);
                     break;
                 case MangaFormat.Image:
                     existingFile.Pages = 1;
                     break;
                 case MangaFormat.Unknown:
                     existingFile.Pages = 0;
                     break;
                 case MangaFormat.Archive:
                     existingFile.Pages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath);
                     break;
             }
             existingFile.LastModified = File.GetLastWriteTime(info.FullFilePath);
          }
          else
          {
             var file = CreateMangaFile(info);
             if (file == null) return;

             chapter.Files.Add(file);
          }
       }
    }
}
