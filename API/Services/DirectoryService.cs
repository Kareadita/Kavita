using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
       private readonly ILogger<DirectoryService> _logger;
       private static readonly Regex ExcludeDirectories = new Regex(
          @"@eaDir|\.DS_Store",
          RegexOptions.Compiled | RegexOptions.IgnoreCase);
       public static readonly string TempDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "temp");
       public static readonly string LogDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "logs");
       public static readonly string CacheDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "cache");
       public static readonly string CoverImageDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "covers");
       public static readonly string BackupDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "backups");
       public static readonly string StatsDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "stats");

       public DirectoryService(ILogger<DirectoryService> logger)
       {
          _logger = logger;
       }

       /// <summary>
       /// Given a set of regex search criteria, get files in the given path.
       /// </summary>
       /// <remarks>This will always exclude <see cref="Parser.Parser.MacOsMetadataFileStartsWith"/> patterns</remarks>
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
                reSearchPattern.IsMatch(Path.GetExtension(file)) && !Path.GetFileName(file).StartsWith(Parser.Parser.MacOsMetadataFileStartsWith));
       }


       /// <summary>
       /// Returns a list of folders from end of fullPath to rootPath. If a file is passed at the end of the fullPath, it will be ignored.
       ///
       /// Example) (C:/Manga/, C:/Manga/Love Hina/Specials/Omake/) returns [Omake, Specials, Love Hina]
       /// </summary>
       /// <param name="rootPath"></param>
       /// <param name="fullPath"></param>
       /// <returns></returns>
       public static IEnumerable<string> GetFoldersTillRoot(string rootPath, string fullPath)
       {
           var separator = Path.AltDirectorySeparatorChar;
          if (fullPath.Contains(Path.DirectorySeparatorChar))
          {
             fullPath = fullPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
          }

          if (rootPath.Contains(Path.DirectorySeparatorChar))
          {
             rootPath = rootPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
          }



          var path = fullPath.EndsWith(separator) ? fullPath.Substring(0, fullPath.Length - 1) : fullPath;
          var root = rootPath.EndsWith(separator) ? rootPath.Substring(0, rootPath.Length - 1) : rootPath;
          var paths = new List<string>();
          // If a file is at the end of the path, remove it before we start processing folders
          if (Path.GetExtension(path) != string.Empty)
          {
             path = path.Substring(0, path.LastIndexOf(separator));
          }

          while (Path.GetDirectoryName(path) != Path.GetDirectoryName(root))
          {
             var folder = new DirectoryInfo(path).Name;
             paths.Add(folder);
             path = path.Substring(0, path.LastIndexOf(separator));
          }

          return paths;
       }

       public bool Exists(string directory)
       {
          var di = new DirectoryInfo(directory);
          return di.Exists;
       }

       public static IEnumerable<string> GetFiles(string path, string searchPatternExpression = "",
          SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
          if (searchPatternExpression != string.Empty)
          {
             if (!Directory.Exists(path)) return ImmutableList<string>.Empty;
             var reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
             return Directory.EnumerateFiles(path, "*", searchOption)
                .Where(file =>
                   reSearchPattern.IsMatch(file) && !file.StartsWith(Parser.Parser.MacOsMetadataFileStartsWith));
          }

          return !Directory.Exists(path) ? Array.Empty<string>() : Directory.GetFiles(path);
       }

       public void CopyFileToDirectory(string fullFilePath, string targetDirectory)
       {
           try
           {
               var fileInfo = new FileInfo(fullFilePath);
               if (fileInfo.Exists)
               {
                   fileInfo.CopyTo(Path.Join(targetDirectory, fileInfo.Name), true);
               }
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "There was a critical error when copying {File} to {Directory}", fullFilePath, targetDirectory);
           }
       }

       /// <summary>
       /// Copies a Directory with all files and subdirectories to a target location
       /// </summary>
       /// <param name="sourceDirName"></param>
       /// <param name="destDirName"></param>
       /// <param name="searchPattern">Defaults to *, meaning all files</param>
       /// <returns></returns>
       /// <exception cref="DirectoryNotFoundException"></exception>
       public static bool CopyDirectoryToDirectory(string sourceDirName, string destDirName, string searchPattern = "")
       {
         if (string.IsNullOrEmpty(sourceDirName)) return false;

         // Get the subdirectories for the specified directory.
         var dir = new DirectoryInfo(sourceDirName);

         if (!dir.Exists)
         {
           throw new DirectoryNotFoundException(
             "Source directory does not exist or could not be found: "
             + sourceDirName);
         }

         var dirs = dir.GetDirectories();

         // If the destination directory doesn't exist, create it.
         ExistOrCreate(destDirName);

         // Get the files in the directory and copy them to the new location.
         var files = GetFilesWithExtension(dir.FullName, searchPattern).Select(n => new FileInfo(n));
         foreach (var file in files)
         {
           var tempPath = Path.Combine(destDirName, file.Name);
           file.CopyTo(tempPath, false);
         }

         // If copying subdirectories, copy them and their contents to new location.
         foreach (var subDir in dirs)
         {
           var tempPath = Path.Combine(destDirName, subDir.Name);
           CopyDirectoryToDirectory(subDir.FullName, tempPath);
         }

         return true;
       }



       public static string[] GetFilesWithExtension(string path, string searchPatternExpression = "")
       {
          if (searchPatternExpression != string.Empty)
          {
             return GetFilesWithCertainExtensions(path, searchPatternExpression).ToArray();
          }

          return !Directory.Exists(path) ? Array.Empty<string>() : Directory.GetFiles(path);
       }

       /// <summary>
       /// Returns the total number of bytes for a given set of full file paths
       /// </summary>
       /// <param name="paths"></param>
       /// <returns>Total bytes</returns>
       public static long GetTotalSize(IEnumerable<string> paths)
       {
          return paths.Sum(path => new FileInfo(path).Length);
       }

       /// <summary>
       /// Returns true if the path exists and is a directory. If path does not exist, this will create it. Returns false in all fail cases.
       /// </summary>
       /// <param name="directoryPath"></param>
       /// <returns></returns>
       public static bool ExistOrCreate(string directoryPath)
       {
          var di = new DirectoryInfo(directoryPath);
          if (di.Exists) return true;
          try
          {
             Directory.CreateDirectory(directoryPath);
          }
          catch (Exception)
          {
             return false;
          }
          return true;
       }

       /// <summary>
       /// Deletes all files within the directory, then the directory itself.
       /// </summary>
       /// <param name="directoryPath"></param>
       public static void ClearAndDeleteDirectory(string directoryPath)
       {
          if (!Directory.Exists(directoryPath)) return;

          DirectoryInfo di = new DirectoryInfo(directoryPath);

          ClearDirectory(directoryPath);

          di.Delete(true);
       }

       /// <summary>
       /// Deletes all files within the directory.
       /// </summary>
       /// <param name="directoryPath"></param>
       /// <returns></returns>
       public static void ClearDirectory(string directoryPath)
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

       /// <summary>
       /// Copies files to a destination directory. If the destination directory doesn't exist, this will create it.
       /// </summary>
       /// <param name="filePaths"></param>
       /// <param name="directoryPath"></param>
       /// <param name="prepend">An optional string to prepend to the target file's name</param>
       /// <returns></returns>
       public bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath, string prepend = "")
       {
           ExistOrCreate(directoryPath);
           string currentFile = null;
           try
           {
               foreach (var file in filePaths)
               {
                   currentFile = file;
                   var fileInfo = new FileInfo(file);
                   if (fileInfo.Exists)
                   {
                       fileInfo.CopyTo(Path.Join(directoryPath, prepend + fileInfo.Name));
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

       public async Task<byte[]> ReadFileAsync(string path)
       {
          if (!File.Exists(path)) return Array.Empty<byte>();
          return await File.ReadAllBytesAsync(path);
       }


       /// <summary>
       /// Finds the highest directories from a set of MangaFiles
       /// </summary>
       /// <param name="libraryFolders">List of top level folders which files belong to</param>
       /// <param name="filePaths">List of file paths that belong to libraryFolders</param>
       /// <returns></returns>
       public static Dictionary<string, string> FindHighestDirectoriesFromFiles(IEnumerable<string> libraryFolders, IList<string> filePaths)
       {
           var stopLookingForDirectories = false;
           var dirs = new Dictionary<string, string>();
           foreach (var folder in libraryFolders)
           {
               if (stopLookingForDirectories) break;
               foreach (var file in filePaths)
               {
                   if (!file.Contains(folder)) continue;

                   var parts = GetFoldersTillRoot(folder, file).ToList();
                   if (parts.Count == 0)
                   {
                       // Break from all loops, we done, just scan folder.Path (library root)
                       dirs.Add(folder, string.Empty);
                       stopLookingForDirectories = true;
                       break;
                   }

                   var fullPath = Path.Join(folder, parts.Last());
                   if (!dirs.ContainsKey(fullPath))
                   {
                       dirs.Add(fullPath, string.Empty);
                   }
               }
           }

           return dirs;
       }


       /// <summary>
       /// Recursively scans files and applies an action on them. This uses as many cores the underlying PC has to speed
       /// up processing.
       /// </summary>
       /// <param name="root">Directory to scan</param>
       /// <param name="action">Action to apply on file path</param>
       /// <param name="searchPattern">Regex pattern to search against</param>
       /// <param name="logger"></param>
       /// <exception cref="ArgumentException"></exception>
       public static int TraverseTreeParallelForEach(string root, Action<string> action, string searchPattern, ILogger logger)
       {
          //Count of files traversed and timer for diagnostic output
            var fileCount = 0;

            // Determine whether to parallelize file processing on each folder based on processor count.
            //var procCount = Environment.ProcessorCount;

            // Data structure to hold names of subfolders to be examined for files.
            var dirs = new Stack<string>();

            if (!Directory.Exists(root)) {
                   throw new ArgumentException("The directory doesn't exist");
            }
            dirs.Push(root);

            while (dirs.Count > 0) {
               var currentDir = dirs.Pop();
               IEnumerable<string> subDirs;
               string[] files;

               try {
                  subDirs = Directory.GetDirectories(currentDir).Where(path => ExcludeDirectories.Matches(path).Count == 0);
               }
               // Thrown if we do not have discovery permission on the directory.
               catch (UnauthorizedAccessException e) {
                  Console.WriteLine(e.Message);
                  logger.LogError(e, "Unauthorized access on {Directory}", currentDir);
                  continue;
               }
               // Thrown if another process has deleted the directory after we retrieved its name.
               catch (DirectoryNotFoundException e) {
                  Console.WriteLine(e.Message);
                  logger.LogError(e, "Directory not found on {Directory}", currentDir);
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
               // Otherwise, execute sequentially. Files are opened and processed
               // synchronously but this could be modified to perform async I/O.
               try {
                  // if (files.Length < procCount) {
                  //    foreach (var file in files) {
                  //       action(file);
                  //       fileCount++;
                  //    }
                  // }
                  // else {
                  //    Parallel.ForEach(files, () => 0, (file, _, localCount) =>
                  //                                 { action(file);
                  //                                   return ++localCount;
                  //                                 },
                  //                     (c) => {
                  //                        Interlocked.Add(ref fileCount, c);
                  //                     });
                  // }
                  foreach (var file in files) {
                     action(file);
                     fileCount++;
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
               foreach (var str in subDirs)
                  dirs.Push(str);
            }

            return fileCount;
        }

       /// <summary>
       /// Attempts to delete the files passed to it. Swallows exceptions.
       /// </summary>
       /// <param name="files">Full path of files to delete</param>
       public static void DeleteFiles(IEnumerable<string> files)
       {
           foreach (var file in files)
           {
               try
               {
                   new FileInfo(file).Delete();
               }
               catch (Exception)
               {
                   /* Swallow exception */
               }
           }
       }

        /// <summary>
        /// Returns the human-readable file size for an arbitrary, 64-bit file size
        /// <remarks>The default format is "0.## XB", e.g. "4.2 KB" or "1.43 GB"</remarks>
        /// </summary>
        /// https://www.somacon.com/p576.php
        /// <param name="bytes"></param>
        /// <returns></returns>
       public static string GetHumanReadableBytes(long bytes)
       {
           // Get absolute value
           var absoluteBytes = (bytes < 0 ? -bytes : bytes);
           // Determine the suffix and readable value
           string suffix;
           double readable;
           switch (absoluteBytes)
           {
               // Exabyte
               case >= 0x1000000000000000:
                   suffix = "EB";
                   readable = (bytes >> 50);
                   break;
               // Petabyte
               case >= 0x4000000000000:
                   suffix = "PB";
                   readable = (bytes >> 40);
                   break;
               // Terabyte
               case >= 0x10000000000:
                   suffix = "TB";
                   readable = (bytes >> 30);
                   break;
               // Gigabyte
               case >= 0x40000000:
                   suffix = "GB";
                   readable = (bytes >> 20);
                   break;
               // Megabyte
               case >= 0x100000:
                   suffix = "MB";
                   readable = (bytes >> 10);
                   break;
               // Kilobyte
               case >= 0x400:
                   suffix = "KB";
                   readable = bytes;
                   break;
               default:
                   return bytes.ToString("0 B"); // Byte
           }
           // Divide by 1024 to get fractional value
           readable = (readable / 1024);
           // Return formatted number with suffix
           return readable.ToString("0.## ") + suffix;
       }
    }
}
