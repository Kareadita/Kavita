using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
       private readonly ILogger<DirectoryService> _logger;

       public DirectoryService(ILogger<DirectoryService> logger)
       {
          _logger = logger;
       }

       /// <summary>
       /// Given a set of regex search criteria, get files in the given path. 
       /// </summary>
       /// <param name="path">Directory to search</param>
       /// <param name="searchPatternExpression">Regex version of search pattern (ie \.mp3|\.mp4). Defaults to * meaning all files.</param>
       /// <param name="searchOption">SearchOption to use, defaults to TopDirectoryOnly</param>
       /// <returns>List of file paths</returns>
       private static IEnumerable<string> GetFilesWithCertainExtensions(string path, 
          string searchPatternExpression = "",
          SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
          if (!Directory.Exists(path)) return ImmutableList<string>.Empty;
          var reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
          return Directory.EnumerateFiles(path, "*", searchOption)
             .Where(file =>
                reSearchPattern.IsMatch(Path.GetExtension(file)));
       }
       
       public IEnumerable<string> GetFiles(string path, string searchPatternExpression = "", 
          SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
          if (searchPatternExpression != string.Empty)
          {
             if (!Directory.Exists(path)) return ImmutableList<string>.Empty;
             var reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
             return Directory.EnumerateFiles(path, "*", searchOption)
                .Where(file =>
                   reSearchPattern.IsMatch(file));
          }
          
          return !Directory.Exists(path) ? Array.Empty<string>() : Directory.GetFiles(path);
       }

       public string[] GetFilesWithExtension(string path, string searchPatternExpression = "")
       {
          if (searchPatternExpression != string.Empty)
          {
             return GetFilesWithCertainExtensions(path, searchPatternExpression).ToArray();
          }
          
          return !Directory.Exists(path) ? Array.Empty<string>() : Directory.GetFiles(path);
       }

       public bool ExistOrCreate(string directoryPath)
       {
          var di = new DirectoryInfo(directoryPath);
          if (di.Exists) return true;
          try
          {
             Directory.CreateDirectory(directoryPath);
          }
          catch (Exception ex)
          {
             _logger.LogError(ex, "There was an issue creating directory: {Directory}", directoryPath);
             return false;
          }
          return true;
       }

       public void ClearAndDeleteDirectory(string directoryPath)
       {
          DirectoryInfo di = new DirectoryInfo(directoryPath);

          ClearDirectory(directoryPath);
          
          di.Delete(true);
       }

       public void ClearDirectory(string directoryPath)
       {
          var di = new DirectoryInfo(directoryPath);
          if (!di.Exists) return;

          foreach (var file in di.EnumerateFiles())
          {
             file.Delete(); 
          }
          foreach (var dir in di.EnumerateDirectories())
          {
             dir.Delete(true); 
          }
       }

       public bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath)
       {
          string currentFile = null;
          try
          {
             foreach (var file in filePaths)
             {
                currentFile = file;
                var fileInfo = new FileInfo(file);
                if (fileInfo.Exists)
                {
                   fileInfo.CopyTo(Path.Join(directoryPath, fileInfo.Name));   
                }
                else
                {
                   _logger.LogWarning("Tried to copy {File} but it doesn't exist", file);
                }
                
             }
          }
          catch (Exception ex)
          {
             _logger.LogError(ex, "Unable to copy {File} to {DirectoryPath}", currentFile, directoryPath);
             return false;
          }

          return true;
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
       
       public async Task<ImageDto> ReadImageAsync(string imagePath)
       {
          if (!File.Exists(imagePath))
          {
             _logger.LogError("Image does not exist on disk");
             return null;
          }
          using var image = Image.NewFromFile(imagePath);

          return new ImageDto
          {
             Content = await ReadFileAsync(imagePath),
             Filename = Path.GetFileNameWithoutExtension(imagePath),
             FullPath = Path.GetFullPath(imagePath),
             Width = image.Width,
             Height = image.Height,
             Format = image.Format,
          };
       }

       public async Task<byte[]> ReadFileAsync(string path)
       {
          if (!File.Exists(path)) return Array.Empty<byte>();
          return await File.ReadAllBytesAsync(path);
       }


       /// <summary>
       /// Recursively scans files and applies an action on them. This uses as many cores the underlying PC has to speed
       /// up processing.
       /// </summary>
       /// <param name="root">Directory to scan</param>
       /// <param name="action">Action to apply on file path</param>
       /// <param name="searchPattern">Regex pattern to search against</param>
       /// <exception cref="ArgumentException"></exception>
       public static int TraverseTreeParallelForEach(string root, Action<string> action, string searchPattern)
        {
           //Count of files traversed and timer for diagnostic output
            var fileCount = 0;

            // Determine whether to parallelize file processing on each folder based on processor count.
            var procCount = Environment.ProcessorCount;

            // Data structure to hold names of subfolders to be examined for files.
            var dirs = new Stack<string>();

            if (!Directory.Exists(root)) {
                   throw new ArgumentException("The directory doesn't exist");
            }
            dirs.Push(root);

            while (dirs.Count > 0) {
               var currentDir = dirs.Pop();
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
                  files = GetFilesWithCertainExtensions(currentDir, searchPattern)
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