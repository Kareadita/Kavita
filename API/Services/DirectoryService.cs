using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using API.IO;
using API.Parser;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
       private readonly ILogger<DirectoryService> _logger;
       private readonly ISeriesRepository _seriesRepository;
       private readonly ILibraryRepository _libraryRepository;

       private ConcurrentDictionary<string, ConcurrentBag<ParserInfo>> _scannedSeries;

       public DirectoryService(ILogger<DirectoryService> logger, 
          ISeriesRepository seriesRepository, 
          ILibraryRepository libraryRepository)
       {
          _logger = logger;
          _seriesRepository = seriesRepository;
          _libraryRepository = libraryRepository;
       }
       
       /// <summary>
       /// Given a set of regex search criteria, get files in the given path. 
       /// </summary>
       /// <param name="path">Directory to search</param>
       /// <param name="searchPatternExpression">Regex version of search pattern (ie \.mp3|\.mp4)</param>
       /// <param name="searchOption">SearchOption to use, defaults to TopDirectoryOnly</param>
       /// <returns>List of file paths</returns>
       private static IEnumerable<string> GetFiles(string path, 
          string searchPatternExpression = "",
          SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
          var reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
          return Directory.EnumerateFiles(path, "*", searchOption)
             .Where(file =>
                reSearchPattern.IsMatch(Path.GetExtension(file)));
       }
       
        public IEnumerable<string> ListDirectory(string rootPath)
        {
           if (!Directory.Exists(rootPath)) return ImmutableList<string>.Empty;
            
            var di = new DirectoryInfo(rootPath);
            var dirs = di.GetDirectories()
                .Where(dir => !(dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System)))
                .Select(d => d.Name).ToImmutableList();
            
            
            return dirs;
        }

       public IList<string> ListFiles(string rootPath)
       {
          if (!Directory.Exists(rootPath)) return ImmutableList<string>.Empty;
          return Directory.GetFiles(rootPath);
       }


       /// <summary>
       /// Processes files found during a library scan. Generates a collection of series->volume->files for DB processing later.
       /// </summary>
       /// <param name="path">Path of a file</param>
       private void Process(string path)
       {
          var fileName = Path.GetFileName(path);
          _logger.LogDebug($"Parsing file {fileName}");

          var info = Parser.Parser.Parse(fileName);
          info.FullFilePath = path;
          if (info.Volumes == string.Empty)
          {
             return;
          }

          ConcurrentBag<ParserInfo> newBag = new ConcurrentBag<ParserInfo>();
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
       
       private Series UpdateSeries(string seriesName, ParserInfo[] infos, bool forceUpdate)
       {
          var series = _seriesRepository.GetSeriesByName(seriesName) ?? new Series
          {
             Name = seriesName,
             OriginalName = seriesName,
             SortName = seriesName,
             Summary = "" // TODO: Check if comicInfo.xml in file and parse metadata out.
          };

          var volumes = UpdateVolumes(series, infos, forceUpdate);
          series.Volumes = volumes;
          series.CoverImage = volumes.OrderBy(x => x.Number).FirstOrDefault()?.CoverImage;
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
             NumberOfPages = GetNumberOfPagesFromArchive(info.FullFilePath)
          };
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
          IList<Volume> existingVolumes = _seriesRepository.GetVolumes(series.Id).ToList();

          foreach (var info in infos)
          {
             var existingVolume = existingVolumes.SingleOrDefault(v => v.Name == info.Volumes);
             if (existingVolume != null)
             {
                var existingFile = existingVolume.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
                if (existingFile != null)
                {
                   existingFile.Chapter = Int32.Parse(info.Chapters);
                   existingFile.Format = info.Format;
                   existingFile.NumberOfPages = GetNumberOfPagesFromArchive(info.FullFilePath);
                }
                else
                {
                   existingVolume.Files.Add(CreateMangaFile(info));
                }
                
                volumes.Add(existingVolume);
             }
             else
             {
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
                      Number = Int32.Parse(info.Volumes),
                      Files = new List<MangaFile>()
                      {
                         CreateMangaFile(info)
                      }
                   };
                   volumes.Add(vol);
                }
             }
             
             Console.WriteLine($"Adding volume {volumes.Last().Number} with File: {info.Filename}");
          }

          foreach (var volume in volumes)
          {
             if (forceUpdate || volume.CoverImage == null || !volume.Files.Any())
             {
                var firstFile = volume.Files.OrderBy(x => x.Chapter).FirstOrDefault()?.FilePath;
                volume.CoverImage = ImageProvider.GetCoverImage(firstFile, true);
             }

             volume.Pages = volume.Files.Sum(x => x.NumberOfPages);
          }

          return volumes;
       }

        public void ScanLibrary(int libraryId, bool forceUpdate)
        {
           var sw = Stopwatch.StartNew();
           Library library;
           try
           {
              library = Task.Run(() => _libraryRepository.GetLibraryForIdAsync(libraryId)).Result;
           }
           catch (Exception ex)
           {
              // This usually only fails if user is not authenticated.
              _logger.LogError($"There was an issue fetching Library {libraryId}.", ex);
              return;
           }
           
           _scannedSeries = new ConcurrentDictionary<string, ConcurrentBag<ParserInfo>>();
           _logger.LogInformation($"Beginning scan on {library.Name}");

           var totalFiles = 0;
           foreach (var folderPath in library.Folders)
           {
              try {
                 totalFiles = TraverseTreeParallelForEach(folderPath.Path, (f) =>
                 {
                    try
                    {
                       Process(f);
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
           library.Series = new List<Series>(); // Temp delete everything until we can mark items Unavailable
           foreach (var seriesKey in series.Keys)
           {
              var mangaSeries = UpdateSeries(seriesKey, series[seriesKey].ToArray(), forceUpdate);
              _logger.LogInformation($"Created/Updated series {mangaSeries.Name}");
              library.Series.Add(mangaSeries);
           }
           
           
           
           _libraryRepository.Update(library);
           
           if (_libraryRepository.SaveAll())
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

        public string GetExtractPath(int volumeId)
        {
           return Path.Join(Directory.GetCurrentDirectory(), $"../cache/{volumeId}/");
        }

        /// <summary>
        /// TODO: Delete this method
        /// </summary>
        /// <param name="archivePath"></param>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        private string ExtractArchive(string archivePath, int volumeId)
        {
           if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath))
           {
              _logger.LogError($"Archive {archivePath} could not be found.");
              return "";
           }
           
           var extractPath = GetExtractPath(volumeId);

           if (Directory.Exists(extractPath))
           {
              _logger.LogInformation($"Archive {archivePath} has already been extracted. Returning existing folder.");
              return extractPath;
           }
           
           using ZipArchive archive = ZipFile.OpenRead(archivePath);
           
           // TODO: Throw error if we couldn't extract
           var needsFlattening = archive.Entries.Count > 0 && !Path.HasExtension(archive.Entries.ElementAt(0).FullName);
           if (!archive.HasFiles() && !needsFlattening) return "";

           archive.ExtractToDirectory(extractPath);
           _logger.LogInformation($"Extracting archive to {extractPath}");
           
           if (needsFlattening)
           {
              _logger.LogInformation("Extracted archive is nested in root folder, flattening...");
              new DirectoryInfo(extractPath).Flatten();
           }

           return extractPath;
        }
        
        public string ExtractArchive(string archivePath, string extractPath)
        {
           if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath))
           {
              _logger.LogError($"Archive {archivePath} could not be found.");
              return "";
           }

           if (Directory.Exists(extractPath))
           {
              _logger.LogDebug($"Archive {archivePath} has already been extracted. Returning existing folder.");
              return extractPath;
           }
           
           using ZipArchive archive = ZipFile.OpenRead(archivePath);
           // TODO: Throw error if we couldn't extract
           var needsFlattening = archive.Entries.Count > 0 && !Path.HasExtension(archive.Entries.ElementAt(0).FullName);
           if (!archive.HasFiles() && !needsFlattening) return "";
            
           archive.ExtractToDirectory(extractPath);
           _logger.LogDebug($"Extracting archive to {extractPath}");

           if (!needsFlattening) return extractPath;
           
           _logger.LogInformation("Extracted archive is nested in root folder, flattening...");
           new DirectoryInfo(extractPath).Flatten();

           return extractPath;
        }

        private int GetNumberOfPagesFromArchive(string archivePath)
        {
           if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath))
           {
              _logger.LogError($"Archive {archivePath} could not be found.");
              return 0;
           }
           
           using ZipArchive archive = ZipFile.OpenRead(archivePath);
           return archive.Entries.Count(e => Parser.Parser.IsImage(e.FullName));
        }
        

        public async Task<ImageDto> ReadImageAsync(string imagePath)
        {
           using var image = Image.NewFromFile(imagePath);

           return new ImageDto
           {
              Content = await File.ReadAllBytesAsync(imagePath),
              Filename = Path.GetFileNameWithoutExtension(imagePath),
              FullPath = Path.GetFullPath(imagePath),
              Width = image.Width,
              Height = image.Height,
              Format = image.Format
           };
        }

        /// <summary>
        /// Recursively scans files and applies an action on them. This uses as many cores the underlying PC has to speed
        /// up processing.
        /// </summary>
        /// <param name="root">Directory to scan</param>
        /// <param name="action">Action to apply on file path</param>
        /// <exception cref="ArgumentException"></exception>
        private static int TraverseTreeParallelForEach(string root, Action<string> action)
        {
            //Count of files traversed and timer for diagnostic output
            int fileCount = 0;

            // Determine whether to parallelize file processing on each folder based on processor count.
            int procCount = Environment.ProcessorCount;

            // Data structure to hold names of subfolders to be examined for files.
            Stack<string> dirs = new Stack<string>();

            if (!Directory.Exists(root)) {
                   throw new ArgumentException("The directory doesn't exist");
            }
            dirs.Push(root);

            while (dirs.Count > 0) {
               string currentDir = dirs.Pop();
               string[] subDirs;
               string[] files;

               try {
                  subDirs = Directory.GetDirectories(currentDir);
               }
               // Thrown if we do not have discovery permission on the directory.
               catch (UnauthorizedAccessException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }
               // Thrown if another process has deleted the directory after we retrieved its name.
               catch (DirectoryNotFoundException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }

               try {
                  files = DirectoryService.GetFiles(currentDir, Parser.Parser.MangaFileExtensions)
                     .ToArray();
               }
               catch (UnauthorizedAccessException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }
               catch (DirectoryNotFoundException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }
               catch (IOException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }

               // Execute in parallel if there are enough files in the directory.
               // Otherwise, execute sequentially.Files are opened and processed
               // synchronously but this could be modified to perform async I/O.
               try {
                  if (files.Length < procCount) {
                     foreach (var file in files) {
                        action(file);
                        fileCount++;
                     }
                  }
                  else {
                     Parallel.ForEach(files, () => 0, (file, _, localCount) =>
                                                  { action(file);
                                                    return ++localCount;
                                                  },
                                      (c) => {
                                                Interlocked.Add(ref fileCount, c);
                                      });
                  }
               }
               catch (AggregateException ae) {
                  ae.Handle((ex) => {
                               if (ex is UnauthorizedAccessException) {
                                  // Here we just output a message and go on.
                                  Console.WriteLine(ex.Message);
                                  return true;
                               }
                               // Handle other exceptions here if necessary...

                               return false;
                  });
               }

               // Push the subdirectories onto the stack for traversal.
               // This could also be done before handing the files.
               foreach (string str in subDirs)
                  dirs.Push(str);
            }

            return fileCount;
        }
        
    }
}