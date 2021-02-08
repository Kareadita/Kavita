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

       [DisableConcurrentExecution(timeoutInSeconds: 120)] 
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
                 });
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

           UpdateLibrary(libraryId, series, library);
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

          foreach (var folder in library.Folders) folder.LastScanned = DateTime.Now;
       }

       protected internal void UpsertSeries(Library library, Dictionary<string, List<ParserInfo>> parsedSeries,
          List<Series> allSeries)
       {
          // NOTE: This is a great point to break the parsing into threads and join back. Each thread can take X series.
          var foundSeries = parsedSeries.Keys.ToList();
          _logger.LogDebug($"Found {foundSeries} series.");
          foreach (var seriesKey in parsedSeries.Keys)
          {
             try
             {
                var mangaSeries = ExistingOrDefault(library, allSeries, seriesKey) ?? new Series
                {
                   Name = seriesKey, // NOTE: Should I apply Title casing here 
                   OriginalName = seriesKey,
                   NormalizedName = Parser.Parser.Normalize(seriesKey),
                   SortName = seriesKey,
                   Summary = ""
                };
                mangaSeries.NormalizedName = Parser.Parser.Normalize(mangaSeries.Name);
             
             
                UpdateSeries(ref mangaSeries, parsedSeries[seriesKey].ToArray());
                if (library.Series.Any(s => Parser.Parser.Normalize(s.Name) == mangaSeries.NormalizedName)) continue;
                _logger.LogInformation($"Added series {mangaSeries.Name}");
                library.Series.Add(mangaSeries);

             }
             catch (Exception ex)
             {
                _logger.LogError(ex, $"There was an error during scanning of library. {seriesKey} will be skipped.");
             }
          }
       }

       private string ToTitleCase(string str)
       {
          return _textInfo.ToTitleCase(str);
       }

       private void RemoveSeriesNotOnDisk(IEnumerable<Series> allSeries, Dictionary<string, List<ParserInfo>> series, Library library)
       {
          _logger.LogInformation("Removing any series that are no longer on disk.");
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
          _logger.LogInformation($"Removed {count} series that are no longer on disk");
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
             _logger.LogWarning($"Could not parse from {path}");
             return;
          }
          
          TrackSeries(info);
       }
       
       private void UpdateSeries(ref Series series, ParserInfo[] infos)
       {
          _logger.LogInformation($"Updating entries for {series.Name}. {infos.Length} related files.");
          
          UpdateVolumes(series, infos);
          series.Pages = series.Volumes.Sum(v => v.Pages);
          
          _metadataService.UpdateMetadata(series, _forceUpdate);
          _logger.LogDebug($"Created {series.Volumes.Count} volumes on {series.Name}");
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


       private void UpdateVolumes(Series series, ParserInfo[] infos)
       {
          series.Volumes ??= new List<Volume>();
          _logger.LogDebug($"Updating Volumes for {series.Name}. {infos.Length} related files.");
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