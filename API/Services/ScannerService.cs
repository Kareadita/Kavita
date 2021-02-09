using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Interfaces;
using API.Interfaces.Services;
using API.Parser;
using Hangfire;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("API.Tests")]
namespace API.Services
{
    public class ScannerService : IScannerService
    {
       private readonly IUnitOfWork _unitOfWork;
       private readonly ILogger<ScannerService> _logger;
       private readonly IArchiveService _archiveService;
       private readonly IMetadataService _metadataService;
       private ConcurrentDictionary<string, List<ParserInfo>> _scannedSeries;
       private bool _forceUpdate;
       private readonly TextInfo _textInfo = new CultureInfo("en-US", false).TextInfo;

       public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger, IArchiveService archiveService, 
          IMetadataService metadataService)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
          _metadataService = metadataService;
       }

       [DisableConcurrentExecution(timeoutInSeconds: 120)] 
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
          
          // if (/*_environment.IsProduction() && */!_forceUpdate && Directory.GetLastWriteTime(folder.Path) < folder.LastScanned)
          // {
          //    _logger.LogDebug($"{folder.Path} hasn't been updated since last scan. Skipping.");
          //    skippedFolders += 1;
          //    return true;
          // }
          //
          // return false;
       }

       private void Cleanup()
       {
          _scannedSeries = null;
       }

       [DisableConcurrentExecution(timeoutInSeconds: 360)] 
       public void ScanLibrary(int libraryId, bool forceUpdate)
       {
          _forceUpdate = forceUpdate;
          var sw = Stopwatch.StartNew();
          Cleanup();
           Library library;
           try
           {
              library = Task.Run(() => _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId)).Result;
           }
           catch (Exception ex)
           {
              // This usually only fails if user is not authenticated.
              _logger.LogError($"There was an issue fetching Library {libraryId}.", ex);
              return;
           }
           
           _scannedSeries = new ConcurrentDictionary<string, List<ParserInfo>>();
           _logger.LogInformation($"Beginning scan on {library.Name}. Forcing metadata update: {forceUpdate}");

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
                       ProcessFile(f, folderPath.Path);
                    }
                    catch (FileNotFoundException exception)
                    {
                       _logger.LogError(exception, $"The file {f} could not be found");
                    }
                 }, Parser.Parser.MangaFileExtensions);
              }
              catch (ArgumentException ex) {
                 _logger.LogError(ex, $"The directory '{folderPath.Path}' does not exist");
              }
              
              folderPath.LastScanned = DateTime.Now;
           }

           var scanElapsedTime = sw.ElapsedMilliseconds;
           _logger.LogInformation("Folders Scanned {0} files in {1} milliseconds", totalFiles, scanElapsedTime);
           sw.Restart();
           if (skippedFolders == library.Folders.Count)
           {
              _logger.LogInformation("All Folders were skipped due to no modifications to the directories.");
              _unitOfWork.LibraryRepository.Update(library);
              _logger.LogInformation("Processed {0} files in {1} milliseconds for {2}", totalFiles, sw.ElapsedMilliseconds, library.Name);
              Cleanup();
              return;
           }

           // Remove any series where there were no parsed infos
           var filtered = _scannedSeries.Where(kvp => kvp.Value.Count != 0);
           var series = filtered.ToDictionary(v => v.Key, v => v.Value);

           //UpdateLibrary(libraryId, series, library);
           UpdateLibrary2(libraryId, series);
           _unitOfWork.LibraryRepository.Update(library);

           if (Task.Run(() => _unitOfWork.Complete()).Result)
           {
              
              _logger.LogInformation($"Scan completed on {library.Name}. Parsed {series.Keys.Count} series in {sw.ElapsedMilliseconds} ms.");
           }
           else
           {
              _logger.LogError("There was a critical error that resulted in a failed scan. Please check logs and rescan.");
           }
           
           _logger.LogInformation("Processed {0} files in {1} milliseconds for {2}", totalFiles, sw.ElapsedMilliseconds + scanElapsedTime, library.Name);
       }

       private void UpdateLibrary(int libraryId, Dictionary<string, List<ParserInfo>> parsedSeries, Library library)
       {
          var allSeries = Task.Run(() => _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId)).Result.ToList();
          
          _logger.LogInformation($"Updating Library {library.Name}");
          // Perform DB activities
          UpsertSeries(library, parsedSeries, allSeries);

          // Remove series that are no longer on disk
          RemoveSeriesNotOnDisk(allSeries, parsedSeries, library);

          var updatedSeries = library.Series.ToList();
          foreach (var librarySeries in updatedSeries)
          {
             if (!librarySeries.Volumes.Any())
             {
                library.Series.Remove(librarySeries);
             }
          }

          foreach (var folder in library.Folders) folder.LastScanned = DateTime.Now;
       }

       private void UpdateLibrary2(int libraryId, Dictionary<string, List<ParserInfo>> parsedSeries)
       {
          var library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).Result;
          
          // First, remove any series that are not in parsedSeries list
          var foundSeries = parsedSeries.Select(s => Parser.Parser.Normalize(s.Key)).ToList();
          var missingSeries = library.Series.Where(existingSeries =>
             !foundSeries.Contains(existingSeries.NormalizedName) || !parsedSeries.ContainsKey(existingSeries.Name) ||
             !parsedSeries.ContainsKey(existingSeries.OriginalName));
          var removeCount = 0;
          foreach (var existingSeries in missingSeries)
          {
             library.Series?.Remove(existingSeries);
             removeCount += 1;
          }
          _logger.LogInformation("Removed {RemoveCount} series that are no longer on disk", removeCount);
          
          // Add new series that have parsedInfos
          foreach (var info in parsedSeries)
          {
             var existingSeries =
                library.Series.SingleOrDefault(s => s.NormalizedName == Parser.Parser.Normalize(info.Key)) ??
                new Series()
                {
                   Name = info.Key,
                   OriginalName = info.Key,
                   NormalizedName = Parser.Parser.Normalize(info.Key),
                   SortName = info.Key,
                   Summary = "",
                   Volumes = new List<Volume>()
                };
             existingSeries.NormalizedName = Parser.Parser.Normalize(info.Key);

             if (existingSeries.Id == 0)
             {
                library.Series.Add(existingSeries);
             }

          }
          
          // Now, we only have to deal with series that exist on disk. Let's recalculate the volumes for each series
          foreach (var existingSeries in library.Series)
          {
             _logger.LogInformation("Processing series {SeriesName}", existingSeries.Name);
             UpdateVolumes2(existingSeries, parsedSeries[existingSeries.Name].ToArray());
             existingSeries.Pages = existingSeries.Volumes.Sum(v => v.Pages);
             _metadataService.UpdateMetadata(existingSeries, _forceUpdate);
          }
          
          foreach (var folder in library.Folders) folder.LastScanned = DateTime.Now;
       }

       private void UpdateVolumes2(Series series, ParserInfo[] parsedInfos)
       {
          var startingVolumeCount = series.Volumes.Count;
          // Add new volumes
          foreach (var info in parsedInfos)
          {
             var volume = series.Volumes.SingleOrDefault(s => s.Name == info.Volumes) ?? new Volume()
             {
                Name = info.Volumes,
                Number = (int) Parser.Parser.MinimumNumberFromRange(info.Volumes),
                IsSpecial = false,
                Chapters = new List<Chapter>()
             };
             volume.IsSpecial = volume.Number == 0;
             
             UpdateChapters2(volume, parsedInfos.Where(p => p.Volumes == volume.Name).ToArray());
             volume.Pages = volume.Chapters.Sum(c => c.Pages);
             _metadataService.UpdateMetadata(volume, _forceUpdate);

             if (volume.Id == 0)
             {
                series.Volumes.Add(volume);
             }
          }
          
          // Remove existing volumes that aren't in parsedInfos and volumes that have no chapters
          var existingVolumes = series.Volumes.ToList();
          foreach (var volume in existingVolumes)
          {
             // I can't remove based on chapter count as I haven't updated Chapters  || volume.Chapters.Count == 0
             var hasInfo = parsedInfos.Any(v => v.Volumes == volume.Name);
             if (!hasInfo)
             {
                series.Volumes.Remove(volume);
             }
          }
          
          // Update each volume with Chapters
          // foreach (var volume in series.Volumes)
          // {
          //    UpdateChapters2(volume, parsedInfos.Where(p => p.Volumes == volume.Name).ToArray());
          //    volume.Pages = volume.Chapters.Sum(c => c.Pages);
          //    _metadataService
          // }

          _logger.LogDebug("Updated {SeriesName} volumes from {StartingVolumeCount} to {VolumeCount}", 
             series.Name, startingVolumeCount, series.Volumes.Count);
       }

       private void UpdateChapters2(Volume volume, ParserInfo[] parsedInfos)
       {
          var startingChapters = volume.Chapters.Count;
          // Add new chapters
          foreach (var info in parsedInfos)
          {
             var chapter = volume.Chapters.SingleOrDefault(c => c.Range == info.Chapters) ?? new Chapter()
             {
                Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "",
                Range = info.Chapters,
                Files = new List<MangaFile>()
             };

             chapter.Files = new List<MangaFile>();

             if (chapter.Id == 0)
             {
                volume.Chapters.Add(chapter);
             }
          }
          
          // Add files
          foreach (var info in parsedInfos)
          {
             var chapter = volume.Chapters.SingleOrDefault(c => c.Range == info.Chapters);
             if (chapter == null) continue;
             // I need to reset Files for the first time, hence this work should be done in a spearate loop
             AddOrUpdateFileForChapter(chapter, info);
             chapter.Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "";
             chapter.Range = info.Chapters;
             chapter.Pages = chapter.Files.Sum(f => f.NumberOfPages);
             _metadataService.UpdateMetadata(chapter, _forceUpdate);
          }
          
          
          
          // Remove chapters that aren't in parsedInfos or have no files linked
          var existingChapters = volume.Chapters.ToList();
          foreach (var existingChapter in existingChapters)
          {
             var hasInfo = parsedInfos.Any(v => v.Chapters == existingChapter.Range);
             if (!hasInfo || !existingChapter.Files.Any())
             {
                volume.Chapters.Remove(existingChapter);
             }
          }
          
          _logger.LogDebug("Updated chapters from {StartingChaptersCount} to {ChapterCount}", 
             startingChapters, volume.Chapters.Count);
       }
       

       protected internal void UpsertSeries(Library library, Dictionary<string, List<ParserInfo>> parsedSeries,
          List<Series> allSeries)
       {
          // NOTE: This is a great point to break the parsing into threads and join back. Each thread can take X series.
          foreach (var seriesKey in parsedSeries.Keys)
          {
             try
             {
                var mangaSeries = allSeries.SingleOrDefault(s => Parser.Parser.Normalize(s.Name) == Parser.Parser.Normalize(seriesKey)) ?? new Series
                {
                   Name = seriesKey,
                   OriginalName = seriesKey,
                   NormalizedName = Parser.Parser.Normalize(seriesKey),
                   SortName = seriesKey,
                   Summary = ""
                };
                mangaSeries.NormalizedName = Parser.Parser.Normalize(mangaSeries.Name);


                UpdateSeries(ref mangaSeries, parsedSeries[seriesKey].ToArray());
                if (library.Series.Any(s => Parser.Parser.Normalize(s.Name) == mangaSeries.NormalizedName)) continue;
                _logger.LogInformation("Added series {SeriesName}", mangaSeries.Name);
                library.Series.Add(mangaSeries);

             }
             catch (Exception ex)
             {
                _logger.LogError(ex, "There was an error during scanning of library. {SeriesName} will be skipped", seriesKey);
             }
          }
       }

       private string ToTitleCase(string str)
       {
          return _textInfo.ToTitleCase(str);
       }

       private void RemoveSeriesNotOnDisk(IEnumerable<Series> allSeries, Dictionary<string, List<ParserInfo>> series, Library library)
       {
          // TODO: Need to also remove any series that no longer have Volumes.
          _logger.LogInformation("Removing any series that are no longer on disk");
          var count = 0;
          var foundSeries = series.Select(s => Parser.Parser.Normalize(s.Key)).ToList();
          var missingSeries = allSeries.Where(existingSeries =>
             !foundSeries.Contains(existingSeries.NormalizedName) || !series.ContainsKey(existingSeries.Name) ||
             !series.ContainsKey(existingSeries.OriginalName));
          
          foreach (var existingSeries in missingSeries)
          {
             // Delete series, there is no file to backup any longer. 
             library.Series?.Remove(existingSeries);
             count++;
          }
          
          _logger.LogInformation("Removed {Count} series that are no longer on disk", count);
       }

       private void RemoveVolumesNotOnDisk(Series series)
       {
          var volumes = series.Volumes.ToList();
          foreach (var volume in volumes)
          {
             var chapters = volume.Chapters;
             if (!chapters.Any())
             {
                series.Volumes.Remove(volume);
             }
          }
       }
       

       /// <summary>
       /// Attempts to either add a new instance of a show mapping to the scannedSeries bag or adds to an existing.
       /// </summary>
       /// <param name="info"></param>
       private void TrackSeries(ParserInfo info)
       {
          if (info.Series == string.Empty) return;
          
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
       private void ProcessFile(string path, string rootPath)
       {
          var info = Parser.Parser.Parse(path, rootPath);
          
          if (info == null)
          {
             _logger.LogWarning("Could not parse from {Path}", path);
             return;
          }
          
          TrackSeries(info);
       }
       
       private void UpdateSeries(ref Series series, ParserInfo[] infos)
       {
          _logger.LogInformation("Updating entries for {series.Name}. {infos.Length} related files", series.Name, infos.Length);
          
          
          UpdateVolumes(series, infos);
          //RemoveVolumesNotOnDisk(series);
          //series.Pages = series.Volumes.Sum(v => v.Pages);

          _metadataService.UpdateMetadata(series, _forceUpdate);
          _logger.LogDebug("Created {series.Volumes.Count} volumes on {series.Name}", series.Volumes.Count, series.Name);
       }

       private MangaFile CreateMangaFile(ParserInfo info)
       {
          return new MangaFile()
          {
             FilePath = info.FullFilePath,
             Format = info.Format,
             NumberOfPages = info.Format == MangaFormat.Archive ? _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath): 1
          };
       }
       

       private void UpdateChapters(Volume volume, IList<Chapter> existingChapters, IEnumerable<ParserInfo> infos)
       {
          volume.Chapters = new List<Chapter>();
          var justVolumeInfos = infos.Where(pi => pi.Volumes == volume.Name).ToArray();
          foreach (var info in justVolumeInfos)
          {
             try
             {
                var chapter = existingChapters.SingleOrDefault(c => c.Range == info.Chapters) ??
                              new Chapter()
                              {
                                 Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "",
                                 Range = info.Chapters,
                              };
                
                AddOrUpdateFileForChapter(chapter, info);
                chapter.Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "";
                chapter.Range = info.Chapters;
                
                if (volume.Chapters.All(c => c.Range != info.Chapters))
                {
                   volume.Chapters.Add(chapter);
                }
             }
             catch (Exception ex)
             {
                _logger.LogWarning(ex, $"There was an exception parsing {info.Series} - Volume {volume.Number}'s chapters. Skipping Chapter.");
             }
          }

          foreach (var chapter in volume.Chapters)
          {
             chapter.Pages = chapter.Files.Sum(f => f.NumberOfPages);
             
             _metadataService.UpdateMetadata(chapter, _forceUpdate);
          }
       }

       private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info)
       {
          chapter.Files ??= new List<MangaFile>();
          var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
          if (existingFile != null)
          {
             existingFile.Format = info.Format;
             existingFile.NumberOfPages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath);
          }
          else
          {
             if (info.Format == MangaFormat.Archive)
             {
                chapter.Files.Add(CreateMangaFile(info));
             }
             else
             {
                _logger.LogDebug($"Ignoring {info.Filename}. Non-archives are not supported yet.");
             }
          }
       }

       public static Volume ExistingOrDefault(IList<Volume> existingVolumes, ICollection<Volume> volumes, string volumeName)
       {
          return volumes.SingleOrDefault(v => v.Name == volumeName) ?? existingVolumes.SingleOrDefault(v => v.Name == volumeName);
       }
       
       public static Series ExistingOrDefault(Library library, IEnumerable<Series> allSeries, string seriesName)
       {
          var name = Parser.Parser.Normalize(seriesName);
          library.Series ??= new List<Series>();
          return library.Series.SingleOrDefault(s => Parser.Parser.Normalize(s.Name) == name) ??
                 allSeries.SingleOrDefault(s => Parser.Parser.Normalize(s.Name) == name);
       }


       private void UpdateVolumes(Series series, IReadOnlyCollection<ParserInfo> infos)
       {
          // BUG: If a volume no longer exists, it is not getting deleted. 
          series.Volumes ??= new List<Volume>();
          _logger.LogDebug($"Updating Volumes for {series.Name}. {infos.Count} related files.");
          var existingVolumes = _unitOfWork.SeriesRepository.GetVolumes(series.Id).ToList();

          foreach (var info in infos)
          {
             try
             {
                var volume = ExistingOrDefault(existingVolumes, series.Volumes, info.Volumes) ?? new Volume
                {
                   Name = info.Volumes,
                   Number = (int) Parser.Parser.MinimumNumberFromRange(info.Volumes),
                   IsSpecial = false,
                   Chapters = new List<Chapter>()
                };
                
                if (series.Volumes.Any(v => v.Name == volume.Name)) continue;
                series.Volumes.Add(volume);
                
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, $"There was an exception when creating volume {info.Volumes}. Skipping volume.");
             }
          }

          foreach (var volume in series.Volumes)
          {
             _logger.LogInformation($"Processing {series.Name} - Volume {volume.Name}");
             try
             {
                UpdateChapters(volume, volume.Chapters, infos);
                volume.Pages = volume.Chapters.Sum(c => c.Pages);
                // BUG: This code does not remove chapters that no longer exist! This means leftover chapters exist when not on disk.
                
                _logger.LogDebug($"Created {volume.Chapters.Count} chapters");
             } catch (Exception ex)
             {
                _logger.LogError(ex, $"There was an exception when creating volume {volume.Name}. Skipping volume.");
             } 
          }

          foreach (var volume in series.Volumes)
          {
             _metadataService.UpdateMetadata(volume, _forceUpdate);
          }
       }
    }
}