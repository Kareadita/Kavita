using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
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
       private ConcurrentDictionary<string, List<ParserInfo>> _scannedSeries;
       private readonly NaturalSortComparer _naturalSort;

       public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger, IArchiveService archiveService, 
          IMetadataService metadataService)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
          _metadataService = metadataService;
          _naturalSort = new NaturalSortComparer(true);
       }


       [DisableConcurrentExecution(timeoutInSeconds: 360)] 
       //[AutomaticRetry(Attempts = 0, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
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
          // NOTE: This solution isn't the best, but it has potential. We need to handle a few other cases so it works great. 
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
       //[AutomaticRetry(Attempts = 0, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
       public void ScanLibrary(int libraryId, bool forceUpdate)
       {
          var sw = Stopwatch.StartNew();
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
           
           
           _logger.LogInformation("Beginning scan on {LibraryName}. Forcing metadata update: {ForceUpdate}", library.Name, forceUpdate);
           
           _scannedSeries = new ConcurrentDictionary<string, List<ParserInfo>>();

           var totalFiles = 0;
           var skippedFolders = 0;
           foreach (var folderPath in library.Folders)
           {
              if (ShouldSkipFolderScan(folderPath, ref skippedFolders)) continue;

              try {
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
                 }, Parser.Parser.ArchiveFileExtensions);
              }
              catch (ArgumentException ex) {
                 _logger.LogError(ex, "The directory '{FolderPath}' does not exist", folderPath.Path);
              }
              
              folderPath.LastScanned = DateTime.Now;
           }

           var scanElapsedTime = sw.ElapsedMilliseconds;
           _logger.LogInformation("Folders Scanned {TotalFiles} files in {ElapsedScanTime} milliseconds", totalFiles, scanElapsedTime);
           sw.Restart();
           if (skippedFolders == library.Folders.Count)
           {
              _logger.LogInformation("All Folders were skipped due to no modifications to the directories");
              _unitOfWork.LibraryRepository.Update(library);
              _scannedSeries = null;
              _logger.LogInformation("Processed {TotalFiles} files in {ElapsedScanTime} milliseconds for {LibraryName}", totalFiles, sw.ElapsedMilliseconds, library.Name);
              return;
           }
           
           // Remove any series where there were no parsed infos
           var filtered = _scannedSeries.Where(kvp => kvp.Value.Count != 0);
           var series = filtered.ToDictionary(v => v.Key, v => v.Value);

           UpdateLibrary(library, series);
           _unitOfWork.LibraryRepository.Update(library);

           if (Task.Run(() => _unitOfWork.Complete()).Result)
           {
              _logger.LogInformation("Scan completed on {LibraryName}. Parsed {ParsedSeriesCount} series in {ElapsedScanTime} ms", library.Name, series.Keys.Count, sw.ElapsedMilliseconds);
           }
           else
           {
              _logger.LogError("There was a critical error that resulted in a failed scan. Please check logs and rescan");
           }
           _scannedSeries = null;
           
           _logger.LogInformation("Processed {TotalFiles} files in {ElapsedScanTime} milliseconds for {LibraryName}", totalFiles, sw.ElapsedMilliseconds + scanElapsedTime, library.Name);
           
           // Cleanup any user progress that doesn't exist
           var cleanedUp = Task.Run(() => _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters()).Result;
           if (cleanedUp)
           {
              _logger.LogInformation("Removed all abandoned progress rows");
           }
           else
           {
              _logger.LogWarning("There are abandoned user progress entities in the DB. In Progress activity stream will be skewed");
           }
           
           BackgroundJob.Enqueue(() => _metadataService.RefreshMetadata(libraryId, forceUpdate));
       }

       private void UpdateLibrary(Library library, Dictionary<string, List<ParserInfo>> parsedSeries)
       {
          if (parsedSeries == null) throw new ArgumentNullException(nameof(parsedSeries));
          
          // First, remove any series that are not in parsedSeries list
          var foundSeries = parsedSeries.Select(s => Parser.Parser.Normalize(s.Key)).ToList();
          // var missingSeries = library.Series.Where(existingSeries =>
          //    !foundSeries.Contains(existingSeries.NormalizedName) || !parsedSeries.ContainsKey(existingSeries.Name)
          //     || (existingSeries.LocalizedName != null && !parsedSeries.ContainsKey(existingSeries.LocalizedName))
          //     || !parsedSeries.ContainsKey(existingSeries.OriginalName));

          var missingSeries = library.Series.Where(existingSeries => !existingSeries.NameInList(foundSeries)
                                                                     || !existingSeries.NameInList(parsedSeries.Keys));
          var removeCount = 0;
          foreach (var existingSeries in missingSeries)
          {
             library.Series?.Remove(existingSeries);
             removeCount += 1;
          }
          _logger.LogInformation("Removed {RemoveCount} series that are no longer on disk", removeCount);
          
          // Add new series that have parsedInfos
          foreach (var (key, _) in parsedSeries)
          {
             var existingSeries = library.Series.SingleOrDefault(s => s.NormalizedName == Parser.Parser.Normalize(key));
             if (existingSeries == null)
             {
                existingSeries = new Series()
                {
                   Name = key,
                   OriginalName = key,
                   LocalizedName = key,
                   NormalizedName = Parser.Parser.Normalize(key),
                   SortName = key,
                   Summary = "",
                   Volumes = new List<Volume>()
                };
                library.Series.Add(existingSeries);
             } 
             existingSeries.NormalizedName = Parser.Parser.Normalize(key);
             existingSeries.LocalizedName ??= key;
          }

          // Now, we only have to deal with series that exist on disk. Let's recalculate the volumes for each series
          var librarySeries = library.Series.ToList();
          Parallel.ForEach(librarySeries, (series) =>
          {
             _logger.LogInformation("Processing series {SeriesName}", series.Name);
             UpdateVolumes(series, parsedSeries[series.OriginalName].ToArray());
             series.Pages = series.Volumes.Sum(v => v.Pages);
          });
       }

       private void UpdateVolumes(Series series, ParserInfo[] parsedInfos)
       {
          var startingVolumeCount = series.Volumes.Count;
          // Add new volumes and update chapters per volume
          var distinctVolumes = parsedInfos.Select(p => p.Volumes).Distinct().ToList();
          _logger.LogDebug("Updating {DistinctVolumes} volumes", distinctVolumes.Count);
          foreach (var volumeNumber in distinctVolumes)
          {
             var infos = parsedInfos.Where(p => p.Volumes == volumeNumber).ToArray();
             
             var volume = series.Volumes.SingleOrDefault(s => s.Name == volumeNumber);
             if (volume == null)
             {
                volume = new Volume()
                {
                   Name = volumeNumber,
                   Number = (int) Parser.Parser.MinimumNumberFromRange(volumeNumber),
                   IsSpecial = false,
                   Chapters = new List<Chapter>()
                }; 
                series.Volumes.Add(volume);
             }
             
             // NOTE: I don't think we need this as chapters now handle specials
             volume.IsSpecial = volume.Number == 0 && infos.All(p => p.Chapters == "0" || p.IsSpecial); 
             _logger.LogDebug("Parsing {SeriesName} - Volume {VolumeNumber}", series.Name, volume.Name);

             UpdateChapters(volume, infos);
             volume.Pages = volume.Chapters.Sum(c => c.Pages);
          }

          // Remove existing volumes that aren't in parsedInfos and volumes that have no chapters
          series.Volumes = series.Volumes.Where(v => parsedInfos.Any(p => p.Volumes == v.Name)).ToList();

          _logger.LogDebug("Updated {SeriesName} volumes from {StartingVolumeCount} to {VolumeCount}", 
             series.Name, startingVolumeCount, series.Volumes.Count);
       }

       private void UpdateChapters(Volume volume, ParserInfo[] parsedInfos)
       {
          var startingChapters = volume.Chapters.Count;

          // Add new chapters
          foreach (var info in parsedInfos)
          {
             var specialTreatment = (info.IsSpecial || (info.Volumes == "0" && info.Chapters == "0"));
             // Specials go into their own chapters with Range being their filename and IsSpecial = True. Non-Specials with Vol and Chap as 0
             // also are treated like specials for UI grouping.
             _logger.LogDebug("Adding new chapters, {Series} - Vol {Volume} Ch {Chapter} - Needs Special Treatment? {NeedsSpecialTreatment}", info.Series, info.Volumes, info.Chapters, specialTreatment);
             // NOTE: If there are duplicate files that parse out to be the same but a different series name (but parses to same normalized name ie History's strongest 
             // vs Historys strongest), this code will break and the duplicate will be skipped.
             Chapter chapter = null;
             try
             {
                chapter = specialTreatment
                   ? volume.Chapters.SingleOrDefault(c => c.Range == info.Filename
                                                          || (c.Files.Select(f => f.FilePath)
                                                             .Contains(info.FullFilePath)))
                   : volume.Chapters.SingleOrDefault(c => c.Range == info.Chapters);
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, "{FileName} mapped as '{Series} - Vol {Volume} Ch {Chapter}' is a duplicate, skipping", info.FullFilePath, info.Series, info.Volumes, info.Chapters);
                return;
             }


             if (chapter == null)
             {
                chapter = new Chapter()
                {
                   Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + string.Empty,
                   Range = specialTreatment ? info.Filename : info.Chapters,
                   Files = new List<MangaFile>(),
                   IsSpecial = specialTreatment
                };
                volume.Chapters.Add(chapter);
             }

             chapter.Files ??= new List<MangaFile>();
             chapter.IsSpecial = specialTreatment;
          }
          
          // Add files
          foreach (var info in parsedInfos)
          {
             var specialTreatment = (info.IsSpecial || (info.Volumes == "0" && info.Chapters == "0"));
             Chapter chapter = null;
             try
             {
                chapter = volume.Chapters.SingleOrDefault(c => c.Range == info.Chapters || (specialTreatment && c.Range == info.Filename));
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, "There was an exception parsing chapter. Skipping {SeriesName} Vol {VolumeNumber} Chapter {ChapterNumber} - Special treatment: {NeedsSpecialTreatment}", info.Series, volume.Name, info.Chapters, specialTreatment);
             }
             if (chapter == null) continue;
             AddOrUpdateFileForChapter(chapter, info);
             chapter.Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "";
             chapter.Range = specialTreatment ? info.Filename : info.Chapters;
             chapter.Pages = chapter.Files.Sum(f => f.Pages);
          }
          
          
          
          
          // Remove chapters that aren't in parsedInfos or have no files linked
          var existingChapters = volume.Chapters.ToList();
          foreach (var existingChapter in existingChapters)
          {
             var specialTreatment = (existingChapter.IsSpecial || (existingChapter.Number == "0" && !int.TryParse(existingChapter.Range, out int i)));
             var hasInfo = specialTreatment ? parsedInfos.Any(v => v.Filename == existingChapter.Range) 
                : parsedInfos.Any(v => v.Chapters == existingChapter.Range);
             
             if (!hasInfo || !existingChapter.Files.Any())
             {
                volume.Chapters.Remove(existingChapter);
             }
             else
             {
                // Ensure we remove any files that no longer exist AND order
                existingChapter.Files = existingChapter.Files
                   .Where(f => parsedInfos.Any(p => p.FullFilePath == f.FilePath))
                   .OrderBy(f => f.FilePath, _naturalSort).ToList();
             }
          }
          
          
          
          _logger.LogDebug("Updated chapters from {StartingChaptersCount} to {ChapterCount}", 
             startingChapters, volume.Chapters.Count);
       }

       /// <summary>
       /// Attempts to either add a new instance of a show mapping to the _scannedSeries bag or adds to an existing.
       /// </summary>
       /// <param name="info"></param>
       private void TrackSeries(ParserInfo info)
       {
          if (info.Series == string.Empty) return;
          
          // Check if normalized info.Series already exists and if so, update info to use that name instead
          var normalizedSeries = Parser.Parser.Normalize(info.Series);
          _logger.LogDebug("Checking if we can merge {NormalizedSeries}", normalizedSeries);
          var existingName = _scannedSeries.SingleOrDefault(p => Parser.Parser.Normalize(p.Key) == normalizedSeries)
             .Key;
          if (!string.IsNullOrEmpty(existingName) && info.Series != existingName)
          {
             _logger.LogDebug("Found duplicate parsed infos, merged {Original} into {Merged}", info.Series, existingName);   
             info.Series = existingName;
          }

          _scannedSeries.AddOrUpdate(info.Series, new List<ParserInfo>() {info}, (_, oldValue) =>
          {
             oldValue ??= new List<ParserInfo>();
             if (!oldValue.Contains(info))
             {
                oldValue.Add(info);
             }

             return oldValue;
          });
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
          var info = Parser.Parser.Parse(path, rootPath, type);
          
          if (info == null)
          {
             _logger.LogWarning("[Scanner] Could not parse series from {Path}", path);
             return;
          }
          
          TrackSeries(info);
       }

       private MangaFile CreateMangaFile(ParserInfo info)
       {
          return new MangaFile()
          {
             FilePath = info.FullFilePath,
             Format = info.Format,
             Pages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath)
          };
       }
  
       private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info)
       {
          chapter.Files ??= new List<MangaFile>();
          var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
          if (existingFile != null)
          {
             existingFile.Format = info.Format;
             if (!new FileInfo(existingFile.FilePath).DoesLastWriteMatch(existingFile.LastModified))
             {
                existingFile.Pages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath);
             }
          }
          else
          {
             if (info.Format == MangaFormat.Archive)
             {
                chapter.Files.Add(CreateMangaFile(info));
                existingFile = chapter.Files.Last();
             }
             else
             {
                _logger.LogDebug("Ignoring {Filename}. Non-archives are not supported", info.Filename);
             }
          }

          if (existingFile != null)
          {
             existingFile.LastModified = new FileInfo(existingFile.FilePath).LastWriteTime;
          }
       }
    }
}