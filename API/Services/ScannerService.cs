using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using API.Parser;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{
    public class ScannerService : IScannerService
    {
       private readonly IUnitOfWork _unitOfWork;
       private readonly ILogger<ScannerService> _logger;
       private readonly IArchiveService _archiveService;
       private ConcurrentDictionary<string, List<ParserInfo>> _scannedSeries;

       public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger, IArchiveService archiveService)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
       }

       public void ScanLibraries()
       {
          var libraries = Task.Run(() => _unitOfWork.LibraryRepository.GetLibrariesAsync()).Result.ToList();
          foreach (var lib in libraries)
          {
             ScanLibrary(lib.Id, false);
          }
       }

       public void ScanLibrary(int libraryId, bool forceUpdate)
        {
           
           var sw = Stopwatch.StartNew();
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
           foreach (var folderPath in library.Folders)
           {
              // if (!forceUpdate && Directory.GetLastWriteTime(folderPath.Path) <= folderPath.LastScanned)
              // {
              //    _logger.LogDebug($"{folderPath.Path} hasn't been updated since last scan. Skipping.");
              //    continue;
              // }
              
              try {
                 totalFiles += DirectoryService.TraverseTreeParallelForEach(folderPath.Path, (f) =>
                 {
                    try
                    {
                       ProcessFile(f, folderPath.Path);
                    }
                    catch (FileNotFoundException exception)
                    {
                       _logger.LogError(exception, "The file could not be found");
                    }
                 });
              }
              catch (ArgumentException ex) {
                 _logger.LogError(ex, $"The directory '{folderPath}' does not exist");
              }
           }

           var filtered = _scannedSeries.Where(kvp => kvp.Value.Count != 0);
           var series = filtered.ToImmutableDictionary(v => v.Key, v => v.Value);

           // Perform DB activities
           var allSeries = UpsertSeries(libraryId, forceUpdate, series, library);

           // Remove series that are no longer on disk
           RemoveSeriesNotOnDisk(allSeries, series, library);

           foreach (var folder in library.Folders) folder.LastScanned = DateTime.Now;
           _unitOfWork.LibraryRepository.Update(library);

           if (Task.Run(() => _unitOfWork.Complete()).Result)
           {
              
              _logger.LogInformation($"Scan completed on {library.Name}. Parsed {series.Keys.Count()} series.");
           }
           else
           {
              _logger.LogError("There was a critical error that resulted in a failed scan. Please rescan.");
           }

           _scannedSeries = null;
           _logger.LogInformation("Processed {0} files in {1} milliseconds for {2}", totalFiles, sw.ElapsedMilliseconds, library.Name);
        }

       private List<Series> UpsertSeries(int libraryId, bool forceUpdate, ImmutableDictionary<string, List<ParserInfo>> series, Library library)
       {
          var allSeries = Task.Run(() => _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId)).Result.ToList();
          foreach (var seriesKey in series.Keys)
          {
             var mangaSeries = allSeries.SingleOrDefault(s => s.Name == seriesKey) ?? new Series
             {
                Name = seriesKey,
                OriginalName = seriesKey,
                SortName = seriesKey,
                Summary = ""
             };
             try
             {
                mangaSeries = UpdateSeries(mangaSeries, series[seriesKey].ToArray(), forceUpdate);
                _logger.LogInformation($"Created/Updated series {mangaSeries.Name} for {library.Name} library");
                library.Series ??= new List<Series>();
                library.Series.Add(mangaSeries);
             }
             catch (Exception ex)
             {
                _logger.LogError(ex, $"There was an error during scanning of library. {seriesKey} will be skipped.");
             }
          }

          return allSeries;
       }

       private void RemoveSeriesNotOnDisk(List<Series> allSeries, ImmutableDictionary<string, List<ParserInfo>> series, Library library)
       {
          var count = 0;
          foreach (var existingSeries in allSeries)
          {
             if (!series.ContainsKey(existingSeries.Name) || !series.ContainsKey(existingSeries.OriginalName))
             {
                // Delete series, there is no file to backup any longer. 
                library.Series?.Remove(existingSeries);
                count++;
             }
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
          
          _scannedSeries.AddOrUpdate(info.Series, new List<ParserInfo>() {info}, (key, oldValue) =>
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
             _logger.LogInformation($"Could not parse series from {path}");
             return;
          }
          
          TrackSeries(info);
       }
       
       private Series UpdateSeries(Series series, ParserInfo[] infos, bool forceUpdate)
       {
          var volumes = UpdateVolumesWithChapters(series, infos, forceUpdate);
          series.Volumes = volumes;
          series.Pages = volumes.Sum(v => v.Pages);
          if (ShouldFindCoverImage(forceUpdate, series.CoverImage))
          {
             var firstCover = volumes.OrderBy(x => x.Number).FirstOrDefault(x => x.Number != 0);
             if (firstCover == null && volumes.Any())
             {
                firstCover = volumes.FirstOrDefault(x => x.Number == 0);
             }
             series.CoverImage = firstCover?.CoverImage;
          }
          if (string.IsNullOrEmpty(series.Summary) || forceUpdate)
          {
             series.Summary = "";
          }
          

          return series;
       }

       private MangaFile CreateMangaFile(ParserInfo info)
       {
          _logger.LogDebug($"Creating File Entry for {info.FullFilePath}");
          
          return new MangaFile()
          {
             FilePath = info.FullFilePath,
             Format = info.Format,
             NumberOfPages = info.Format == MangaFormat.Archive ? _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath): 1
          };
       }

       private bool ShouldFindCoverImage(bool forceUpdate, byte[] coverImage)
       {
          return forceUpdate || coverImage == null || !coverImage.Any();
       }

       
       
       

       /// <summary>
       /// Creates or Updates volumes for a given series
       /// </summary>
       /// <param name="series">Series wanting to be updated</param>
       /// <param name="infos">Parser info</param>
       /// <param name="forceUpdate">Forces metadata update (cover image) even if it's already been set.</param>
       /// <returns>Updated Volumes for given series</returns>
       private ICollection<Volume> UpdateVolumes(Series series, ParserInfo[] infos, bool forceUpdate)
       {
          ICollection<Volume> volumes = new List<Volume>();
          IList<Volume> existingVolumes = _unitOfWork.SeriesRepository.GetVolumes(series.Id).ToList();
       
          //var justVolumes = infos.Select(pi => pi.Chapters == "0");
          
       
          foreach (var info in infos)
          {
             var existingVolume = existingVolumes.SingleOrDefault(v => v.Name == info.Volumes);
             if (existingVolume != null)
             {
                //var existingFile = existingVolume.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
                var existingFile = new MangaFile();
                if (existingFile != null)
                {
                   //existingFile.Chapter = Parser.Parser.MinimumNumberFromRange(info.Chapters);
                   existingFile.Format = info.Format;
                   existingFile.NumberOfPages = _archiveService.GetNumberOfPagesFromArchive(info.FullFilePath);
                }
                else
                {
                   if (info.Format == MangaFormat.Archive)
                   {
                      // existingVolume.Files.Add(CreateMangaFile(info));   
                   }
                   else
                   {
                      _logger.LogDebug($"Ignoring {info.Filename} as it is not an archive.");
                   }
                   
                }
                
                volumes.Add(existingVolume);
             }
             else
             {
                // Create New Volume
                existingVolume = volumes.SingleOrDefault(v => v.Name == info.Volumes);
                if (existingVolume != null)
                {
                   //existingVolume.Files.Add(CreateMangaFile(info));
                }
                else
                {
                   var vol = new Volume()
                   {
                      Name = info.Volumes,
                      Number = Parser.Parser.MinimumNumberFromRange(info.Volumes),
                      // Files = new List<MangaFile>()
                      // {
                      //    CreateMangaFile(info)
                      // }
                   };
                   volumes.Add(vol);
                }
             }
             
             _logger.LogInformation($"Adding volume {volumes.Last().Number} with File: {info.Filename}");
          }
       
          foreach (var volume in volumes)
          {
             // if (forceUpdate || volume.CoverImage == null || !volume.Files.Any())
             // {
             //    var firstFile = volume.Files.OrderBy(x => x.Chapter).FirstOrDefault();
             //    if (firstFile != null) volume.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
             // }
       
             //volume.Pages = volume.Files.Sum(x => x.NumberOfPages);
          }
       
          return volumes;
       }


       /// <summary>
       /// 
       /// </summary>
       /// <param name="volume"></param>
       /// <param name="infos"></param>
       /// <param name="forceUpdate"></param>
       /// <returns></returns>
       private ICollection<Chapter> UpdateChapters(Volume volume, IEnumerable<ParserInfo> infos, bool forceUpdate)
       {
          var chapters = new List<Chapter>();

          foreach (var info in infos)
          {
             volume.Chapters ??= new List<Chapter>();
             var chapter = volume.Chapters.SingleOrDefault(c => c.Range == info.Chapters) ??
                                   chapters.SingleOrDefault(v => v.Range == info.Chapters) ?? 
                                   new Chapter()
                                   {
                                      Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "",
                                      Range = info.Chapters,
                                   };
             
             chapter.Files ??= new List<MangaFile>();
             var existingFile = chapter?.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
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
                   _logger.LogDebug($"Ignoring {info.Filename} as it is not an archive.");
                }
                   
             }

             chapter.Number = Parser.Parser.MinimumNumberFromRange(info.Chapters) + "";
             chapter.Range = info.Chapters;

             chapters.Add(chapter);
          }

          foreach (var chapter in chapters)
          {
             chapter.Pages = chapter.Files.Sum(f => f.NumberOfPages);
             
             if (ShouldFindCoverImage(forceUpdate, chapter.CoverImage))
             {
                var firstFile = chapter?.Files.OrderBy(x => x.Chapter).FirstOrDefault();
                if (firstFile != null) chapter.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
             }
          }
          
          return chapters;
       }


       private ICollection<Volume> UpdateVolumesWithChapters(Series series, ParserInfo[] infos, bool forceUpdate)
       {
          ICollection<Volume> volumes = new List<Volume>();
          IList<Volume> existingVolumes = _unitOfWork.SeriesRepository.GetVolumes(series.Id).ToList();

          foreach (var info in infos)
          {
             var volume = (existingVolumes.SingleOrDefault(v => v.Name == info.Volumes) ??
                           volumes.SingleOrDefault(v => v.Name == info.Volumes)) ?? new Volume
             {
                Name = info.Volumes,
                Number = Parser.Parser.MinimumNumberFromRange(info.Volumes),
             };


             var chapters = UpdateChapters(volume, infos.Where(pi => pi.Volumes == volume.Name).ToArray(), forceUpdate);
             volume.Chapters = chapters;
             volume.Pages = chapters.Sum(c => c.Pages);
             volumes.Add(volume);
          }

          foreach (var volume in volumes)
          {
             if (ShouldFindCoverImage(forceUpdate, volume.CoverImage))
             {
                // TODO: Create a custom sorter for Chapters so it's consistent across the application
                var firstChapter = volume.Chapters.OrderBy(x => Double.Parse(x.Number)).FirstOrDefault();
                var firstFile = firstChapter?.Files.OrderBy(x => x.Chapter).FirstOrDefault();
                if (firstFile != null) volume.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
             }
          }
          
          return volumes;
       }

       

       public void ScanSeries(int libraryId, int seriesId)
       {
          throw new NotImplementedException();
       }
       

    }
}