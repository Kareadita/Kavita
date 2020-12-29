using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces;
using API.Parser;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
       private readonly ILogger<DirectoryService> _logger;
       private static readonly string MangaFileExtensions = @"\.cbz|\.cbr|\.png|\.jpeg|\.jpg|\.zip|\.rar";
       private ConcurrentDictionary<string, ConcurrentBag<ParserInfo>> _scannedSeries;

       public DirectoryService(ILogger<DirectoryService> logger)
       {
          _logger = logger;
       }
       
       /// <summary>
       /// Given a set of regex search criteria, get files in the given path. 
       /// </summary>
       /// <param name="path">Directory to search</param>
       /// <param name="searchPatternExpression">Regex version of search pattern (ie \.mp3|\.mp4)</param>
       /// <param name="searchOption">SearchOption to use, defaults to TopDirectoryOnly</param>
       /// <returns>List of file paths</returns>
       public static IEnumerable<string> GetFiles(string path, 
          string searchPatternExpression = "",
          SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
          Regex reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
          return Directory.EnumerateFiles(path, "*", searchOption)
             .Where(file =>
                reSearchPattern.IsMatch(Path.GetExtension(file)));
       }

       
       

       /// <summary>
        /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
        /// </summary>
        /// <param name="rootPath">Absolute path </param>
        /// <returns>List of folder names</returns>
        public IEnumerable<string> ListDirectory(string rootPath)
        {
           if (!Directory.Exists(rootPath)) return ImmutableList<string>.Empty;
            
            var di = new DirectoryInfo(rootPath);
            var dirs = di.GetDirectories()
                .Where(dir => !(dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System)))
                .Select(d => d.Name).ToImmutableList();
            
            
            return dirs;
        }

       // TODO: Refactor API layer to use this 
       public IEnumerable<DirectoryInfo> ListDirectories(string rootPath)
       {
          if (!Directory.Exists(rootPath)) return ImmutableList<DirectoryInfo>.Empty;
            
          var di = new DirectoryInfo(rootPath);
          var dirs = di.GetDirectories()
             .Where(dir => !(dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System)))
             .ToImmutableList();
            
            
          return dirs;
       }

       private void Process(string path)
       {
          if (Directory.Exists(path))
          {
             DirectoryInfo di = new DirectoryInfo(path);
             Console.WriteLine($"Parsing directory {di.Name}");

             var seriesName = Parser.Parser.ParseSeries(di.Name);
             if (string.IsNullOrEmpty(seriesName))
             {
                return;
             }
            
             // We don't need ContainsKey, this is a race condition. We can replace with TryAdd instead
             if (!_scannedSeries.ContainsKey(seriesName))
             {
                _scannedSeries.TryAdd(seriesName, new ConcurrentBag<ParserInfo>());
             }
          }
          else
          {
             var fileName = Path.GetFileName(path);
             Console.WriteLine($"Parsing file {fileName}");
             
             var info = Parser.Parser.Parse(fileName);
             if (info.Volumes != string.Empty)
             {
                ConcurrentBag<ParserInfo> tempBag;
                ConcurrentBag<ParserInfo> newBag = new ConcurrentBag<ParserInfo>();
                if (_scannedSeries.TryGetValue(info.Series, out tempBag))
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
             
             
          }
       }

        public void ScanLibrary(LibraryDto library)
        {
           _scannedSeries = new ConcurrentDictionary<string, ConcurrentBag<ParserInfo>>();
           //Dictionary<string, IList<ParserInfo>> series = new Dictionary<string, IList<ParserInfo>>();   
           _logger.LogInformation($"Beginning scan on {library.Name}");
           foreach (var folderPath in library.Folders)
           {
              try {
                 // // Temporarily, let's build a simple scanner then optimize to parallelization
                 //
                 // // First, let's see if there are any files in rootPath
                 // var files = GetFiles(folderPath, MangaFileExtensions);
                 //
                 // foreach (var file in files)
                 // {
                 //     // These do not have a folder, so we need to parse them directly
                 //     var parserInfo = Parser.Parser.Parse(file);
                 //     Console.WriteLine(parserInfo);
                 // }
                 //
                 // // Get Directories
                 // var directories = ListDirectories(folderPath);
                 // foreach (var directory in directories)
                 // {
                 //    _logger.LogDebug($"Scanning {directory.Name}");
                 //    var parsedSeries = Parser.Parser.ParseSeries(directory.Name);
                 //
                 //    // For now, let's skip directories we can't parse information out of. (we are assuming one level deep root)
                 //    if (string.IsNullOrEmpty(parsedSeries)) continue;
                 //    
                 //    _logger.LogDebug($"Parsed Series: {parsedSeries}");
                 //
                 //    if (!series.ContainsKey(parsedSeries))
                 //    {
                 //       series[parsedSeries] = new List<ParserInfo>();
                 //    }
                 //    
                 //    var foundFiles = GetFiles(directory.FullName, MangaFileExtensions);
                 //    foreach (var foundFile in foundFiles)
                 //    {
                 //       var info = Parser.Parser.Parse(foundFile);
                 //       if (info.Volumes != string.Empty)
                 //       {
                 //          series[parsedSeries].Add(info);
                 //       }
                 //    }
                 // }


                 TraverseTreeParallelForEach(folderPath, (f) =>
                 {
                    // Exceptions are no-ops.
                    try
                    {
                       Process(f);
                       //ProcessManga(folderPath, f);
                    }
                    catch (FileNotFoundException) {}
                    catch (IOException) {}
                    catch (UnauthorizedAccessException) {}
                    catch (SecurityException) {}
                    // Display the filename.
                    Console.WriteLine(f);
                 });
              }
              catch (ArgumentException ex) {
                 _logger.LogError(ex, "There was an issue scanning the directory");
                 _logger.LogError($"The directory '{folderPath}' does not exist");
              }
           }

           // var filtered = series.Where(kvp => kvp.Value.Count > 0);
           // series = filtered.ToDictionary(v => v.Key, v => v.Value);
           // Console.WriteLine(series);
           
           // var filtered = _scannedSeries.Where(kvp => kvp.Value.Count > 0);
           // series = filtered.ToDictionary(v => v.Key, v => v.Value);
           // Console.WriteLine(series);
           var filtered = _scannedSeries.Where(kvp => !kvp.Value.IsEmpty);
           var series = filtered.ToImmutableDictionary(v => v.Key, v => v.Value);
           Console.WriteLine(series);
           
           // TODO: Perform DB activities on ImmutableDictionary
           
           
           //_logger.LogInformation($"Scan completed on {library.Name}. Parsed {series.Keys.Count} series.");
           _logger.LogInformation($"Scan completed on {library.Name}. Parsed {series.Keys.Count()} series.");
           _scannedSeries = null;
           
           
        }

        private static void ProcessManga(string folderPath, string filename)
        {
           Console.WriteLine($"[ProcessManga] Folder: {folderPath}");
           
            Console.WriteLine($"Found {filename}");
            var series = Parser.Parser.ParseSeries(filename);
            if (series == string.Empty)
            {
               series = Parser.Parser.ParseSeries(folderPath);
            }
            Console.WriteLine($"Series: {series}");
        }
        
        public static void TraverseTreeParallelForEach(string root, Action<string> action)
         {
            //Count of files traversed and timer for diagnostic output
            int fileCount = 0;
            var sw = Stopwatch.StartNew();

            // Determine whether to parallelize file processing on each folder based on processor count.
            int procCount = Environment.ProcessorCount;

            // Data structure to hold names of subfolders to be examined for files.
            Stack<string> dirs = new Stack<string>();

            if (!Directory.Exists(root)) {
                   throw new ArgumentException();
            }
            dirs.Push(root);

            while (dirs.Count > 0) {
               string currentDir = dirs.Pop();
               string[] subDirs = {};
               string[] files = {};

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
                  //files = Directory.GetFiles(currentDir, "*.")
                  files = DirectoryService.GetFiles(currentDir, MangaFileExtensions)
                     .ToArray();
                  //files = Directory.GetFiles(currentDir);
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
                     Parallel.ForEach(files, () => 0, (file, loopState, localCount) =>
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

            // For diagnostic purposes.
            Console.WriteLine("Processed {0} files in {1} milliseconds", fileCount, sw.ElapsedMilliseconds);
         }
    }
}