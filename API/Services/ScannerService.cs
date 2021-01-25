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
       private ConcurrentDictionary<string, ConcurrentBag<ParserInfo>> _scannedSeries;

       public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
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
           
           _scannedSeries = new ConcurrentDictionary<string, ConcurrentBag<ParserInfo>>();
           _logger.LogInformation($"Beginning scan on {library.Name}. Forcing metadata update: {forceUpdate}");

           var totalFiles = 0;
           foreach (var folderPath in library.Folders)
           {
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
           
           var filtered = _scannedSeries.Where(kvp => !kvp.Value.IsEmpty);
           var series = filtered.ToImmutableDictionary(v => v.Key, v => v.Value);

           // Perform DB activities
           var allSeries = UpsertSeries(libraryId, forceUpdate, series, library);

           // Remove series that are no longer on disk
           RemoveSeriesNotOnDisk(allSeries, series, library);

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

       private List<Series> UpsertSeries(int libraryId, bool forceUpdate, ImmutableDictionary<string, ConcurrentBag<ParserInfo>> series, Library library)
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

       private void RemoveSeriesNotOnDisk(List<Series> allSeries, ImmutableDictionary<string, ConcurrentBag<ParserInfo>> series, Library library)
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
       public void TrackSeries(ParserInfo info)
       {
          if (info.Series == string.Empty) return;
          
          ConcurrentBag<ParserInfo> newBag = new ConcurrentBag<ParserInfo>();
          // Use normalization for key lookup due to parsing disparities
          var existingKey = _scannedSeries.Keys.SingleOrDefault(k => k.ToLower() == info.Series.ToLower());
          if (existingKey != null) info.Series = existingKey;
          if (_scannedSeries.TryGetValue(info.Series, out var tempBag))
          {
             var existingInfos = tempBag.ToArray();
             foreach (var existingInfo in existingInfos)
             {
                newBag.Add(existingInfo);
             }
          }
          else
          {
             tempBag = new ConcurrentBag<ParserInfo>();
          }

          newBag.Add(info);

          if (!_scannedSeries.TryUpdate(info.Series, newBag, tempBag))
          {
             _scannedSeries.TryAdd(info.Series, newBag);
          }
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
          var volumes = UpdateVolumes(series, infos, forceUpdate);
          series.Volumes = volumes;
          series.Pages = volumes.Sum(v => v.Pages);
          if (series.CoverImage == null || forceUpdate)
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
             series.Summary = ""; // TODO: Check if comicInfo.xml in file and parse metadata out.   
          }
          

          return series;
       }

       private MangaFile CreateMangaFile(ParserInfo info)
       {
          _logger.LogDebug($"Creating File Entry for {info.FullFilePath}");
          
          int.TryParse(info.Chapters, out var chapter);
          _logger.LogDebug($"Found Chapter: {chapter}");
          return new MangaFile()
          {
             FilePath = info.FullFilePath,
             Chapter = chapter,
             Format = info.Format,
             NumberOfPages = info.Format == MangaFormat.Archive ? GetNumberOfPagesFromArchive(info.FullFilePath): 1
          };
       }

       // TODO: Implement Test
       public int MinimumNumberFromRange(string range)
       {
          var tokens = range.Split("-");
          return Int32.Parse(tokens.Length >= 1 ? tokens[0] : range);
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

          foreach (var info in infos)
          {
             var existingVolume = existingVolumes.SingleOrDefault(v => v.Name == info.Volumes);
             if (existingVolume != null)
             {
                var existingFile = existingVolume.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
                if (existingFile != null)
                {
                   existingFile.Chapter = MinimumNumberFromRange(info.Chapters);
                   existingFile.Format = info.Format;
                   existingFile.NumberOfPages = GetNumberOfPagesFromArchive(info.FullFilePath);
                }
                else
                {
                   if (info.Format == MangaFormat.Archive)
                   {
                      existingVolume.Files.Add(CreateMangaFile(info));   
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
                   existingVolume.Files.Add(CreateMangaFile(info));
                }
                else
                {
                   var vol = new Volume()
                   {
                      Name = info.Volumes,
                      Number = MinimumNumberFromRange(info.Volumes),
                      Files = new List<MangaFile>()
                      {
                         CreateMangaFile(info)
                      }
                   };
                   volumes.Add(vol);
                }
             }
             
             _logger.LogInformation($"Adding volume {volumes.Last().Number} with File: {info.Filename}");
          }

          foreach (var volume in volumes)
          {
             if (forceUpdate || volume.CoverImage == null || !volume.Files.Any())
             {
                var firstFile = volume.Files.OrderBy(x => x.Chapter).FirstOrDefault();
                if (firstFile != null) volume.CoverImage = GetCoverImage(firstFile.FilePath, true); // ZIPFILE
             }

             volume.Pages = volume.Files.Sum(x => x.NumberOfPages);
          }

          return volumes;
       }

      


       public void ScanSeries(int libraryId, int seriesId)
        {
           throw new NotImplementedException();
        }

        private int GetNumberOfPagesFromArchive(string archivePath)
        {
           if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath))
           {
              _logger.LogError($"Archive {archivePath} could not be found.");
              return 0;
           }
           
           _logger.LogDebug($"Getting Page numbers from  {archivePath}");
           
           using ZipArchive archive = ZipFile.OpenRead(archivePath); // ZIPFILE
           return archive.Entries.Count(e => Parser.Parser.IsImage(e.FullName));
        }

        /// <summary>
        /// Generates byte array of cover image.
        /// Given a path to a compressed file (zip, rar, cbz, cbr, etc), will ensure the first image is returned unless
        /// a folder.extension exists in the root directory of the compressed file.
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="createThumbnail">Create a smaller variant of file extracted from archive. Archive images are usually 1MB each.</param>
        /// <returns></returns>
        public byte[] GetCoverImage(string filepath, bool createThumbnail = false)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath) || !Parser.Parser.IsArchive(filepath)) return Array.Empty<byte>();

                _logger.LogDebug($"Extracting Cover image from {filepath}");
                using ZipArchive archive = ZipFile.OpenRead(filepath);
                if (!archive.HasFiles()) return Array.Empty<byte>();

                var folder = archive.Entries.SingleOrDefault(x => Path.GetFileNameWithoutExtension(x.Name).ToLower() == "folder");
                var entries = archive.Entries.Where(x => Path.HasExtension(x.FullName) && Parser.Parser.IsImage(x.FullName)).OrderBy(x => x.FullName).ToList();
                ZipArchiveEntry entry;
                
                if (folder != null)
                {
                    entry = folder;
                } else if (!entries.Any())
                {
                    return Array.Empty<byte>();
                }
                else
                {
                    entry = entries[0];
                }


                if (createThumbnail)
                {
                    try
                    {
                        using var stream = entry.Open();
                        var thumbnail = Image.ThumbnailStream(stream, 320);
                        return thumbnail.WriteToBuffer(".jpg");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was a critical error and prevented thumbnail generation.");
                    }
                }
                
                return ExtractEntryToImage(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when reading archive stream.");
                return Array.Empty<byte>();
            }
        }
        
        private static byte[] ExtractEntryToImage(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            return data;
        }

    }
}