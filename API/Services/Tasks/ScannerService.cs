using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using API.Parser;
using Hangfire;
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
       private ConcurrentDictionary<string, List<ParserInfo>> _scannedSeries;
       private readonly NaturalSortComparer _naturalSort;

       public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger, IArchiveService archiveService, 
          IMetadataService metadataService, IBookService bookService)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
          _metadataService = metadataService;
          _bookService = bookService;
          _naturalSort = new NaturalSortComparer();
       }


       [DisableConcurrentExecution(timeoutInSeconds: 360)]
       [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public void ScanLibraries()
       {
          var libraries = Task.Run(() => _unitOfWork.LibraryRepository.GetLibrariesAsync()).Result.ToList();
          foreach (var lib in libraries)
          {
             ScanLibrary(lib.Id, false);
          }
       }

       private bool ShouldSkipFolderScan(FolderPath folder, ref int skippedFolders)
       {
          // NOTE: The only way to skip folders is if Directory hasn't been modified, we aren't doing a forcedUpdate and version hasn't changed between scans.
          return false;

          // if (!_forceUpdate && Directory.GetLastWriteTime(folder.Path) < folder.LastScanned)
          // {
          //    _logger.LogDebug("{FolderPath} hasn't been modified since last scan. Skipping", folder.Path);
          //    skippedFolders += 1;
          //    return true;
          // }
          
          //return false;
       }

       [DisableConcurrentExecution(360)]
       [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public void ScanLibrary(int libraryId, bool forceUpdate)
       {
          var sw = Stopwatch.StartNew();
          _scannedSeries = new ConcurrentDictionary<string, List<ParserInfo>>();
          Library library;
           try
           {
              library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).GetAwaiter().GetResult();
           }
           catch (Exception ex)
           {
              // This usually only fails if user is not authenticated.
              _logger.LogError(ex, "There was an issue fetching Library {LibraryId}", libraryId);
              return;
           }
           
           
           var series = ScanLibrariesForSeries(forceUpdate, library, sw, out var totalFiles, out var scanElapsedTime);
           UpdateLibrary(library, series);
           
           _unitOfWork.LibraryRepository.Update(library);
           if (Task.Run(() => _unitOfWork.Complete()).Result)
           {
              _logger.LogInformation("Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}", totalFiles, series.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, library.Name);
           }
           else
           {
              _logger.LogCritical("There was a critical error that resulted in a failed scan. Please check logs and rescan");
           }

           CleanupUserProgress();

           BackgroundJob.Enqueue(() => _metadataService.RefreshMetadata(libraryId, forceUpdate));
       }

       /// <summary>
       /// Remove any user progress rows that no longer exist since scan library ran and deleted series/volumes/chapters
       /// </summary>
       private void CleanupUserProgress()
       {
          var cleanedUp = Task.Run(() => _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters()).Result;
          _logger.LogInformation("Removed {Count} abandoned progress rows", cleanedUp);
       }

       private Dictionary<string, List<ParserInfo>> ScanLibrariesForSeries(bool forceUpdate, Library library, Stopwatch sw, out int totalFiles,
          out long scanElapsedTime)
       {
          _logger.LogInformation("Beginning scan on {LibraryName}. Forcing metadata update: {ForceUpdate}", library.Name,
             forceUpdate);
          totalFiles = 0;
          var skippedFolders = 0;
          foreach (var folderPath in library.Folders)
          {
             if (ShouldSkipFolderScan(folderPath, ref skippedFolders)) continue;

             // NOTE: we can refactor this to allow all filetypes and handle everything in the ProcessFile to allow mixed library types.
             var searchPattern = Parser.Parser.ArchiveFileExtensions;
             if (library.Type == LibraryType.Book)
             {
                searchPattern = Parser.Parser.BookFileExtensions;
             }

             try
             {
                totalFiles += DirectoryService.TraverseTreeParallelForEach(folderPath.Path, (f) =>
                {
                   try
                   {
                      ProcessFile(f, folderPath.Path, library.Type);
                   }
                   catch (FileNotFoundException exception)
                   {
                      _logger.LogError(exception, "The file {Filename} could not be found", f);
                   }
                }, searchPattern, _logger);
             }
             catch (ArgumentException ex)
             {
                _logger.LogError(ex, "The directory '{FolderPath}' does not exist", folderPath.Path);
             }

             folderPath.LastScanned = DateTime.Now;
          }

          scanElapsedTime = sw.ElapsedMilliseconds;
          _logger.LogInformation("Folders Scanned {TotalFiles} files in {ElapsedScanTime} milliseconds", totalFiles,
             scanElapsedTime);
          sw.Restart();
          if (skippedFolders == library.Folders.Count)
          {
             _logger.LogInformation("All Folders were skipped due to no modifications to the directories");
             _unitOfWork.LibraryRepository.Update(library);
             _scannedSeries = null;
             _logger.LogInformation("Processed {TotalFiles} files in {ElapsedScanTime} milliseconds for {LibraryName}",
                totalFiles, sw.ElapsedMilliseconds, library.Name);
             return new Dictionary<string, List<ParserInfo>>();
          }
          
          return SeriesWithInfos(_scannedSeries);
       }

       /// <summary>
       /// Returns any series where there were parsed infos
       /// </summary>
       /// <param name="scannedSeries"></param>
       /// <returns></returns>
       private static Dictionary<string, List<ParserInfo>> SeriesWithInfos(IDictionary<string, List<ParserInfo>> scannedSeries)
       {
          var filtered = scannedSeries.Where(kvp => kvp.Value.Count > 0);
          var series = filtered.ToDictionary(v => v.Key, v => v.Value);
          return series;
       }

       
       private void UpdateLibrary(Library library, Dictionary<string, List<ParserInfo>> parsedSeries)
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
                _logger.LogDebug("Removed {SeriesName}", s.Name);
             }
          }
          
          
          // Add new series that have parsedInfos
          foreach (var (key, infos) in parsedSeries)
          {
             // Key is normalized already
             Series existingSeries;
             try
             {
                existingSeries = library.Series.SingleOrDefault(s => s.NormalizedName == key || Parser.Parser.Normalize(s.OriginalName) == key);
             }
             catch (Exception e)
             {
                _logger.LogCritical(e, "There are multiple series that map to normalized key {Key}. You can manually delete the entity via UI and rescan to fix it", key);
                var duplicateSeries = library.Series.Where(s => s.NormalizedName == key || Parser.Parser.Normalize(s.OriginalName) == key).ToList();
                foreach (var series in duplicateSeries)
                {
                   _logger.LogCritical("{Key} maps with {Series}", key, series.OriginalName);
                   
                }

                continue;
             }
             if (existingSeries == null)
             {
                existingSeries = DbFactory.Series(infos[0].Series);
                library.Series.Add(existingSeries);
             }
             
             existingSeries.NormalizedName = Parser.Parser.Normalize(existingSeries.Name);
             existingSeries.OriginalName ??= infos[0].Series;
          }

          // Now, we only have to deal with series that exist on disk. Let's recalculate the volumes for each series
          var librarySeries = library.Series.ToList();
          Parallel.ForEach(librarySeries, (series) =>
          {
             try
             {
                _logger.LogInformation("Processing series {SeriesName}", series.OriginalName);
                UpdateVolumes(series, parsedSeries[Parser.Parser.Normalize(series.OriginalName)].ToArray());
                series.Pages = series.Volumes.Sum(v => v.Pages);
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, "There was an exception updating volumes for {SeriesName}", series.Name);
             }
          });
       }

       public IEnumerable<Series> FindSeriesNotOnDisk(ICollection<Series> existingSeries, Dictionary<string, List<ParserInfo>> parsedSeries)
       {
          var foundSeries = parsedSeries.Select(s => s.Key).ToList();
          return existingSeries.Where(es => !es.NameInList(foundSeries));
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
                m => m.NormalizedName.Equals(s.NormalizedName))).ToList();

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
             
             // NOTE: Instead of creating and adding? Why Not Merge a new volume into an existing, so no matter what, new properties,etc get propagated?
             
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

       /// <summary>
       /// Attempts to either add a new instance of a show mapping to the _scannedSeries bag or adds to an existing.
       /// </summary>
       /// <param name="info"></param>
       private void TrackSeries(ParserInfo info)
       {
          if (info.Series == string.Empty) return;
          
          // Check if normalized info.Series already exists and if so, update info to use that name instead
          info.Series = MergeName(_scannedSeries, info);
          
          _scannedSeries.AddOrUpdate(Parser.Parser.Normalize(info.Series), new List<ParserInfo>() {info}, (_, oldValue) =>
          {
             oldValue ??= new List<ParserInfo>();
             if (!oldValue.Contains(info))
             {
                oldValue.Add(info);
             }

             return oldValue;
          });
       }

       public string MergeName(ConcurrentDictionary<string,List<ParserInfo>> collectedSeries, ParserInfo info)
       {
          var normalizedSeries = Parser.Parser.Normalize(info.Series);
          _logger.LogDebug("Checking if we can merge {NormalizedSeries}", normalizedSeries);
          var existingName = collectedSeries.SingleOrDefault(p => Parser.Parser.Normalize(p.Key) == normalizedSeries)
             .Key;
          // BUG: We are comparing info.Series against a normalized string. They should never match. (This can cause series to not delete or parse correctly after a rename) 
          if (!string.IsNullOrEmpty(existingName)) //  && info.Series != existingName
          {
             _logger.LogDebug("Found duplicate parsed infos, merged {Original} into {Merged}", info.Series, existingName);
             return existingName;
          }

          return info.Series;
       }

       /// <summary>
       /// Processes files found during a library scan.
       /// Populates a collection of <see cref="ParserInfo"/> for DB updates later.
       /// </summary>
       /// <param name="path">Path of a file</param>
       /// <param name="rootPath"></param>
       /// <param name="type">Library type to determine parsing to perform</param>
       private void ProcessFile(string path, string rootPath, LibraryType type)
       {
          ParserInfo info;
          
          if (type == LibraryType.Book && Parser.Parser.IsEpub(path))
          {
             info = _bookService.ParseInfo(path);
          }
          else
          {
             info = Parser.Parser.Parse(path, rootPath, type);
          }

          if (info == null)
          {
             _logger.LogWarning("[Scanner] Could not parse series from {Path}", path);
             return;
          }
          
          if (type == LibraryType.Book && Parser.Parser.IsEpub(path) && Parser.Parser.ParseVolume(info.Series) != "0")
          {
             info = Parser.Parser.Parse(path, rootPath, type);
             var info2 = _bookService.ParseInfo(path);
             info.Merge(info2);
          }
          
          TrackSeries(info);
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
             case MangaFormat.Book:
             {
                return new MangaFile()
                {
                   FilePath = info.FullFilePath,
                   Format = info.Format,
                   Pages = _bookService.GetNumberOfPages(info.FullFilePath)
                };
             }
             default:
                _logger.LogWarning("[Scanner] Ignoring {Filename}. Non-archives are not supported", info.Filename);
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
             if (!existingFile.HasFileBeenModified() && existingFile.Pages > 0)
             {
                existingFile.Pages = existingFile.Format == MangaFormat.Book 
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
                existingFile = chapter.Files.Last();
             }
          }

          if (existingFile != null)
          {
             existingFile.LastModified = new FileInfo(existingFile.FilePath).LastWriteTime;
          }
       }
    }
}