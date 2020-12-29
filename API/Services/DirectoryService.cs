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
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
       private readonly ILogger<DirectoryService> _logger;
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


       /// <summary>
       /// Processes files found during a library scan. 
       /// </summary>
       /// <param name="path"></param>
       private void Process(string path)
       {
          // NOTE: In current implementation, this never runs. We can probably remove. 
          if (Directory.Exists(path))
          {
             DirectoryInfo di = new DirectoryInfo(path);
             _logger.LogDebug($"Parsing directory {di.Name}");

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
             _logger.LogDebug($"Parsing file {fileName}");
             
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
           _logger.LogInformation($"Beginning scan on {library.Name}");
           
           foreach (var folderPath in library.Folders)
           {
              try {
                 TraverseTreeParallelForEach(folderPath, (f) =>
                 {
                    // Exceptions are no-ops.
                    try
                    {
                       Process(f);
                    }
                    catch (FileNotFoundException) {}
                    catch (IOException) {}
                    catch (UnauthorizedAccessException) {}
                    catch (SecurityException) {}
                 });
              }
              catch (ArgumentException ex) {
                 _logger.LogError(ex, "The directory '{folderPath}' does not exist");
              }
           }
           
           var filtered = _scannedSeries.Where(kvp => !kvp.Value.IsEmpty);
           var series = filtered.ToImmutableDictionary(v => v.Key, v => v.Value);

           // TODO: Perform DB activities on ImmutableDictionary
           
           
           _logger.LogInformation($"Scan completed on {library.Name}. Parsed {series.Keys.Count()} series.");
           _scannedSeries = null;
        }

        private static void TraverseTreeParallelForEach(string root, Action<string> action)
         {
            //Count of files traversed and timer for diagnostic output
            int fileCount = 0;
            var sw = Stopwatch.StartNew();

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

            // For diagnostic purposes.
            Console.WriteLine("Processed {0} files in {1} milliseconds", fileCount, sw.ElapsedMilliseconds);
         }
    }
}