﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Extensions;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public interface IDirectoryService
    {
        IFileSystem FileSystem { get; }
        string CacheDirectory { get; }
        string CoverImageDirectory { get; }
        string LogDirectory { get; }
        string TempDirectory { get; }
        string ConfigDirectory { get; }
        string SiteThemeDirectory { get; }
        /// <summary>
        /// Original BookmarkDirectory. Only used for resetting directory. Use <see cref="ServerSettingKey.BackupDirectory"/> for actual path.
        /// </summary>
        string BookmarkDirectory { get; }
        /// <summary>
        /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
        /// </summary>
        /// <param name="rootPath">Absolute path of directory to scan.</param>
        /// <returns>List of folder names</returns>
        IEnumerable<string> ListDirectory(string rootPath);
        Task<byte[]> ReadFileAsync(string path);
        bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath, string prepend = "");
        bool Exists(string directory);
        void CopyFileToDirectory(string fullFilePath, string targetDirectory);
        int TraverseTreeParallelForEach(string root, Action<string> action, string searchPattern, ILogger logger);
        bool IsDriveMounted(string path);
        bool IsDirectoryEmpty(string path);
        long GetTotalSize(IEnumerable<string> paths);
        void ClearDirectory(string directoryPath);
        void ClearAndDeleteDirectory(string directoryPath);
        string[] GetFilesWithExtension(string path, string searchPatternExpression = "");
        bool CopyDirectoryToDirectory(string sourceDirName, string destDirName, string searchPattern = "");

        Dictionary<string, string> FindHighestDirectoriesFromFiles(IEnumerable<string> libraryFolders,
            IList<string> filePaths);

        IEnumerable<string> GetFoldersTillRoot(string rootPath, string fullPath);

        IEnumerable<string> GetFiles(string path, string fileNameRegex = "", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        bool ExistOrCreate(string directoryPath);
        void DeleteFiles(IEnumerable<string> files);
        void RemoveNonImages(string directoryName);
        void Flatten(string directoryName);
        Task<bool> CheckWriteAccess(string directoryName);
    }
    public class DirectoryService : IDirectoryService
    {
        public IFileSystem FileSystem { get; }
        public string CacheDirectory { get; }
        public string CoverImageDirectory { get; }
        public string LogDirectory { get; }
        public string TempDirectory { get; }
        public string ConfigDirectory { get; }
        public string BookmarkDirectory { get; }
        public string SiteThemeDirectory { get; }
        private readonly ILogger<DirectoryService> _logger;

       private static readonly Regex ExcludeDirectories = new Regex(
          @"@eaDir|\.DS_Store|\.qpkg",
          RegexOptions.Compiled | RegexOptions.IgnoreCase);
       public static readonly string BackupDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "backups");

       public DirectoryService(ILogger<DirectoryService> logger, IFileSystem fileSystem)
       {
           _logger = logger;
           FileSystem = fileSystem;
           CoverImageDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "covers");
           CacheDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "cache");
           LogDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "logs");
           TempDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "temp");
           ConfigDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config");
           BookmarkDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "bookmarks");
           SiteThemeDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "themes");
       }

       /// <summary>
       /// Given a set of regex search criteria, get files in the given path.
       /// </summary>
       /// <remarks>This will always exclude <see cref="Parser.Parser.MacOsMetadataFileStartsWith"/> patterns</remarks>
       /// <param name="path">Directory to search</param>
       /// <param name="searchPatternExpression">Regex version of search pattern (ie \.mp3|\.mp4). Defaults to * meaning all files.</param>
       /// <param name="searchOption">SearchOption to use, defaults to TopDirectoryOnly</param>
       /// <returns>List of file paths</returns>
       private IEnumerable<string> GetFilesWithCertainExtensions(string path,
          string searchPatternExpression = "",
          SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
          if (!FileSystem.Directory.Exists(path)) return ImmutableList<string>.Empty;
          var reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);

          return FileSystem.Directory.EnumerateFiles(path, "*", searchOption)
             .Where(file =>
                reSearchPattern.IsMatch(FileSystem.Path.GetExtension(file)) && !FileSystem.Path.GetFileName(file).StartsWith(Parser.Parser.MacOsMetadataFileStartsWith));
       }


       /// <summary>
       /// Returns a list of folders from end of fullPath to rootPath. If a file is passed at the end of the fullPath, it will be ignored.
       ///
       /// Example) (C:/Manga/, C:/Manga/Love Hina/Specials/Omake/) returns [Omake, Specials, Love Hina]
       /// </summary>
       /// <param name="rootPath"></param>
       /// <param name="fullPath"></param>
       /// <returns></returns>
       public IEnumerable<string> GetFoldersTillRoot(string rootPath, string fullPath)
       {
           var separator = FileSystem.Path.AltDirectorySeparatorChar;
          if (fullPath.Contains(FileSystem.Path.DirectorySeparatorChar))
          {
             fullPath = fullPath.Replace(FileSystem.Path.DirectorySeparatorChar, FileSystem.Path.AltDirectorySeparatorChar);
          }

          if (rootPath.Contains(Path.DirectorySeparatorChar))
          {
             rootPath = rootPath.Replace(FileSystem.Path.DirectorySeparatorChar, FileSystem.Path.AltDirectorySeparatorChar);
          }



          var path = fullPath.EndsWith(separator) ? fullPath.Substring(0, fullPath.Length - 1) : fullPath;
          var root = rootPath.EndsWith(separator) ? rootPath.Substring(0, rootPath.Length - 1) : rootPath;
          var paths = new List<string>();
          // If a file is at the end of the path, remove it before we start processing folders
          if (FileSystem.Path.GetExtension(path) != string.Empty)
          {
             path = path.Substring(0, path.LastIndexOf(separator));
          }

          while (FileSystem.Path.GetDirectoryName(path) != Path.GetDirectoryName(root))
          {
              var folder = FileSystem.DirectoryInfo.FromDirectoryName(path).Name;
             paths.Add(folder);
             path = path.Substring(0, path.LastIndexOf(separator));
          }

          return paths;
       }

       /// <summary>
       /// Does Directory Exist
       /// </summary>
       /// <param name="directory"></param>
       /// <returns></returns>
       public bool Exists(string directory)
       {
           var di = FileSystem.DirectoryInfo.FromDirectoryName(directory);
           return di.Exists;
       }

       /// <summary>
       /// Get files given a path.
       /// </summary>
       /// <remarks>This will automatically filter out restricted files, like MacOsMetadata files</remarks>
       /// <param name="path"></param>
       /// <param name="fileNameRegex">An optional regex string to search against. Will use file path to match against.</param>
       /// <param name="searchOption">Defaults to top level directory only, can be given all to provide recursive searching</param>
       /// <returns></returns>
       public IEnumerable<string> GetFiles(string path, string fileNameRegex = "", SearchOption searchOption = SearchOption.TopDirectoryOnly)
       {
           if (!FileSystem.Directory.Exists(path)) return ImmutableList<string>.Empty;

          if (fileNameRegex != string.Empty)
          {
              var reSearchPattern = new Regex(fileNameRegex, RegexOptions.IgnoreCase);
             return FileSystem.Directory.EnumerateFiles(path, "*", searchOption)
                .Where(file =>
                {
                    var fileName = FileSystem.Path.GetFileName(file);
                    return reSearchPattern.IsMatch(fileName) &&
                           !fileName.StartsWith(Parser.Parser.MacOsMetadataFileStartsWith);
                });
          }

          return FileSystem.Directory.EnumerateFiles(path, "*", searchOption).Where(file =>
              !FileSystem.Path.GetFileName(file).StartsWith(Parser.Parser.MacOsMetadataFileStartsWith));
       }

       /// <summary>
       /// Copies a file into a directory. Does not maintain parent folder of file.
       /// Will create target directory if doesn't exist. Automatically overwrites what is there.
       /// </summary>
       /// <param name="fullFilePath"></param>
       /// <param name="targetDirectory"></param>
       public void CopyFileToDirectory(string fullFilePath, string targetDirectory)
       {
           try
           {
               var fileInfo = FileSystem.FileInfo.FromFileName(fullFilePath);
               if (!fileInfo.Exists) return;

               ExistOrCreate(targetDirectory);
               fileInfo.CopyTo(FileSystem.Path.Join(targetDirectory, fileInfo.Name), true);
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "There was a critical error when copying {File} to {Directory}", fullFilePath, targetDirectory);
           }
       }

       /// <summary>
       /// Copies all files and subdirectories within a directory to a target location
       /// </summary>
       /// <param name="sourceDirName">Directory to copy from. Does not copy the parent folder</param>
       /// <param name="destDirName">Destination to copy to. Will be created if doesn't exist</param>
       /// <param name="searchPattern">Defaults to all files</param>
       /// <returns>If was successful</returns>
       /// <exception cref="DirectoryNotFoundException">Thrown when source directory does not exist</exception>
       public bool CopyDirectoryToDirectory(string sourceDirName, string destDirName, string searchPattern = "")
       {
         if (string.IsNullOrEmpty(sourceDirName)) return false;

         // Get the subdirectories for the specified directory.
         var dir = FileSystem.DirectoryInfo.FromDirectoryName(sourceDirName);

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
         var files = GetFilesWithExtension(dir.FullName, searchPattern).Select(n => FileSystem.FileInfo.FromFileName(n));
         foreach (var file in files)
         {
           var tempPath = FileSystem.Path.Combine(destDirName, file.Name);
           file.CopyTo(tempPath, false);
         }

         // If copying subdirectories, copy them and their contents to new location.
         foreach (var subDir in dirs)
         {
           var tempPath = FileSystem.Path.Combine(destDirName, subDir.Name);
           CopyDirectoryToDirectory(subDir.FullName, tempPath);
         }

         return true;
       }

       /// <summary>
       /// Checks if the root path of a path exists or not.
       /// </summary>
       /// <param name="path"></param>
       /// <returns></returns>
       public bool IsDriveMounted(string path)
       {
           return FileSystem.DirectoryInfo.FromDirectoryName(FileSystem.Path.GetPathRoot(path) ?? string.Empty).Exists;
       }


        /// <summary>
        /// Checks if the root path of a path is empty or not.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool IsDirectoryEmpty(string path)
        {
            return FileSystem.Directory.Exists(path) && !FileSystem.Directory.EnumerateFileSystemEntries(path).Any();
        }

        public string[] GetFilesWithExtension(string path, string searchPatternExpression = "")
       {
           if (searchPatternExpression != string.Empty)
           {
               return GetFilesWithCertainExtensions(path, searchPatternExpression).ToArray();
           }

           return !FileSystem.Directory.Exists(path) ? Array.Empty<string>() : FileSystem.Directory.GetFiles(path);
       }

       /// <summary>
       /// Returns the total number of bytes for a given set of full file paths
       /// </summary>
       /// <param name="paths"></param>
       /// <returns>Total bytes</returns>
       public long GetTotalSize(IEnumerable<string> paths)
       {
           return paths.Sum(path => FileSystem.FileInfo.FromFileName(path).Length);
       }

       /// <summary>
       /// Returns true if the path exists and is a directory. If path does not exist, this will create it. Returns false in all fail cases.
       /// </summary>
       /// <param name="directoryPath"></param>
       /// <returns></returns>
       public bool ExistOrCreate(string directoryPath)
       {
           var di = FileSystem.DirectoryInfo.FromDirectoryName(directoryPath);
          if (di.Exists) return true;
          try
          {
              FileSystem.Directory.CreateDirectory(directoryPath);
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
       public void ClearAndDeleteDirectory(string directoryPath)
       {
           if (!FileSystem.Directory.Exists(directoryPath)) return;

          var di = FileSystem.DirectoryInfo.FromDirectoryName(directoryPath);

          ClearDirectory(directoryPath);

          di.Delete(true);
       }

       /// <summary>
       /// Deletes all files and folders within the directory path
       /// </summary>
       /// <param name="directoryPath"></param>
       /// <returns></returns>
       public void ClearDirectory(string directoryPath)
       {
           var di = FileSystem.DirectoryInfo.FromDirectoryName(directoryPath);
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
       /// <remarks>If a file already exists in dest, this will rename as (2). It does not support multiple iterations of this. Overwriting is not supported.</remarks>
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
                   var fileInfo = FileSystem.FileInfo.FromFileName(file);
                   if (fileInfo.Exists)
                   {
                       // TODO: I need to handle if file already exists and allow either an overwrite or prepend (2) to it
                       try
                       {
                           fileInfo.CopyTo(FileSystem.Path.Join(directoryPath, prepend + fileInfo.Name));
                       }
                       catch (IOException ex)
                       {
                           _logger.LogError(ex, "File copy, dest already exists. Appending (2)");
                           fileInfo.CopyTo(FileSystem.Path.Join(directoryPath, prepend + FileSystem.Path.GetFileNameWithoutExtension(fileInfo.Name) + " (2)" + FileSystem.Path.GetExtension(fileInfo.Name)));
                       }
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

       /// <summary>
       /// Lists all directories in a root path. Will exclude Hidden or System directories.
       /// </summary>
       /// <param name="rootPath"></param>
       /// <returns></returns>
       public IEnumerable<string> ListDirectory(string rootPath)
        {
            if (!FileSystem.Directory.Exists(rootPath)) return ImmutableList<string>.Empty;

            var di = FileSystem.DirectoryInfo.FromDirectoryName(rootPath);
            var dirs = di.GetDirectories()
                .Where(dir => !(dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System)))
                .Select(d => d.Name).ToImmutableList();

            return dirs;
        }

       /// <summary>
       /// Reads a file's into byte[]. Returns empty array if file doesn't exist.
       /// </summary>
       /// <param name="path"></param>
       /// <returns></returns>
       public async Task<byte[]> ReadFileAsync(string path)
       {
          if (!FileSystem.File.Exists(path)) return Array.Empty<byte>();
          return await FileSystem.File.ReadAllBytesAsync(path);
       }


       /// <summary>
       /// Finds the highest directories from a set of file paths. Does not return the root path, will always select the highest non-root path.
       /// </summary>
       /// <remarks>If the file paths do not contain anything from libraryFolders, this returns an empty dictionary back</remarks>
       /// <param name="libraryFolders">List of top level folders which files belong to</param>
       /// <param name="filePaths">List of file paths that belong to libraryFolders</param>
       /// <returns></returns>
       public Dictionary<string, string> FindHighestDirectoriesFromFiles(IEnumerable<string> libraryFolders, IList<string> filePaths)
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
       public int TraverseTreeParallelForEach(string root, Action<string> action, string searchPattern, ILogger logger)
       {
            //Count of files traversed and timer for diagnostic output
            var fileCount = 0;


            // Data structure to hold names of subfolders to be examined for files.
            var dirs = new Stack<string>();

            if (!FileSystem.Directory.Exists(root)) {
                throw new ArgumentException("The directory doesn't exist");
            }

            dirs.Push(root);

            while (dirs.Count > 0) {
               var currentDir = dirs.Pop();
               IEnumerable<string> subDirs;
               string[] files;

               try {
                  subDirs = FileSystem.Directory.GetDirectories(currentDir).Where(path => ExcludeDirectories.Matches(path).Count == 0);
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
                                  _logger.LogError(ex, "Unauthorized access on file");
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
       public void DeleteFiles(IEnumerable<string> files)
       {
           foreach (var file in files)
           {
               try
               {
                   FileSystem.FileInfo.FromFileName(file).Delete();
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

        /// <summary>
        /// Removes all files except images from the directory. Includes sub directories.
        /// </summary>
        /// <param name="directoryName">Fully qualified directory</param>
        public void RemoveNonImages(string directoryName)
        {
            DeleteFiles(GetFiles(directoryName, searchOption:SearchOption.AllDirectories).Where(file => !Parser.Parser.IsImage(file)));
        }


        /// <summary>
        /// Flattens all files in subfolders to the passed directory recursively.
        ///
        ///
        /// foo<para />
        /// ├── 1.txt<para />
        /// ├── 2.txt<para />
        /// ├── 3.txt<para />
        /// ├── 4.txt<para />
        /// └── bar<para />
        ///     ├── 1.txt<para />
        ///     ├── 2.txt<para />
        ///     └── 5.txt<para />
        ///
        /// becomes:<para />
        /// foo<para />
        /// ├── 1.txt<para />
        /// ├── 2.txt<para />
        /// ├── 3.txt<para />
        /// ├── 4.txt<para />
        ///     ├── bar_1.txt<para />
        ///     ├── bar_2.txt<para />
        ///     └── bar_5.txt<para />
        /// </summary>
        /// <param name="directoryName">Fully qualified Directory name</param>
        public void Flatten(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName) || !FileSystem.Directory.Exists(directoryName)) return;

            var directory = FileSystem.DirectoryInfo.FromDirectoryName(directoryName);

            var index = 0;
            FlattenDirectory(directory, directory, ref index);
        }

        /// <summary>
        /// Checks whether a directory has write permissions
        /// </summary>
        /// <param name="directoryName">Fully qualified path</param>
        /// <returns></returns>
        public async Task<bool> CheckWriteAccess(string directoryName)
        {
            try
            {
                ExistOrCreate(directoryName);
                await FileSystem.File.WriteAllTextAsync(
                    FileSystem.Path.Join(directoryName, "test.txt"),
                    string.Empty);
            }
            catch (Exception ex)
            {
                ClearAndDeleteDirectory(directoryName);
                return false;
            }

            ClearAndDeleteDirectory(directoryName);
            return true;
        }


        private void FlattenDirectory(IDirectoryInfo root, IDirectoryInfo directory, ref int directoryIndex)
        {
            if (!root.FullName.Equals(directory.FullName))
            {
                var fileIndex = 1;

                foreach (var file in directory.EnumerateFiles().OrderByNatural(file => file.FullName))
                {
                    if (file.Directory == null) continue;
                    var paddedIndex = Parser.Parser.PadZeros(directoryIndex + "");
                    // We need to rename the files so that after flattening, they are in the order we found them
                    var newName = $"{paddedIndex}_{Parser.Parser.PadZeros(fileIndex + "")}{file.Extension}";
                    var newPath = Path.Join(root.FullName, newName);
                    if (!File.Exists(newPath)) file.MoveTo(newPath);
                    fileIndex++;
                }

                directoryIndex++;
            }

            foreach (var subDirectory in directory.EnumerateDirectories().OrderByNatural(d => d.FullName))
            {
                FlattenDirectory(root, subDirectory, ref directoryIndex);
            }
        }
    }
}
