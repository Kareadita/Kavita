using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.DTOs.System;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface IDirectoryService
{
    IFileSystem FileSystem { get; }
    string CacheDirectory { get; }
    string CoverImageDirectory { get; }
    string LogDirectory { get; }
    string TempDirectory { get; }
    string ConfigDirectory { get; }
    string SiteThemeDirectory { get; }
    string FaviconDirectory { get; }
    string LocalizationDirectory { get; }
    string CustomizedTemplateDirectory { get; }
    string TemplateDirectory { get; }
    string PublisherDirectory { get; }
    /// <summary>
    /// Used for caching documents that may need to stay on disk for more than a day
    /// </summary>
    string LongTermCacheDirectory { get; }
    /// <summary>
    /// Original BookmarkDirectory. Only used for resetting directory. Use <see cref="ServerSettingKey.BackupDirectory"/> for actual path.
    /// </summary>
    string BookmarkDirectory { get; }
    /// <summary>
    /// Used for random files needed, like images to check against, list of countries, etc
    /// </summary>
    string AssetsDirectory { get; }
    /// <summary>
    /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
    /// </summary>
    /// <param name="rootPath">Absolute path of directory to scan.</param>
    /// <returns>List of folder names</returns>
    IEnumerable<DirectoryDto> ListDirectory(string rootPath);
    Task<byte[]> ReadFileAsync(string path);
    bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath, string prepend = "");
    bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath, IList<string> newFilenames);
    bool Exists(string directory);
    void CopyFileToDirectory(string fullFilePath, string targetDirectory);
    int TraverseTreeParallelForEach(string root, Action<string> action, string searchPattern, ILogger logger);
    bool IsDriveMounted(string path);
    bool IsDirectoryEmpty(string path);
    long GetTotalSize(IEnumerable<string> paths);
    void ClearDirectory(string directoryPath);
    void ClearAndDeleteDirectory(string directoryPath);
    string[] GetFilesWithExtension(string path, string searchPatternExpression = "");
    bool CopyDirectoryToDirectory(string? sourceDirName, string destDirName, string searchPattern = "");
    Dictionary<string, string> FindHighestDirectoriesFromFiles(IEnumerable<string> libraryFolders,
        IList<string> filePaths);
    string? FindLowestDirectoriesFromFiles(IList<string> libraryFolders,
        IList<string> filePaths);
    IEnumerable<string> GetFoldersTillRoot(string rootPath, string fullPath);
    IEnumerable<string> GetFiles(string path, string fileNameRegex = "", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    bool ExistOrCreate(string directoryPath);
    void DeleteFiles(IEnumerable<string> files);
    void RemoveNonImages(string directoryName);
    void Flatten(string directoryName);
    Task<bool> CheckWriteAccess(string directoryName);
    IEnumerable<string> GetFilesWithCertainExtensions(string path,
        string searchPatternExpression = "",
        SearchOption searchOption = SearchOption.TopDirectoryOnly);
    IEnumerable<string> GetDirectories(string folderPath);
    IEnumerable<string> GetDirectories(string folderPath, GlobMatcher? matcher);
    IEnumerable<string> GetAllDirectories(string folderPath, GlobMatcher? matcher = null);
    string GetParentDirectoryName(string fileOrFolder);
    IList<string> ScanFiles(string folderPath, string fileTypes, GlobMatcher? matcher = null, SearchOption searchOption = SearchOption.AllDirectories);
    DateTime GetLastWriteTime(string folderPath);
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
    public string AssetsDirectory { get; }
    public string SiteThemeDirectory { get; }
    public string FaviconDirectory { get; }
    public string LocalizationDirectory { get; }
    public string CustomizedTemplateDirectory { get; }
    public string TemplateDirectory { get; }
    public string PublisherDirectory { get; }
    public string LongTermCacheDirectory { get; }
    private readonly ILogger<DirectoryService> _logger;
    private const RegexOptions MatchOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase;

    private static readonly Regex ExcludeDirectories = new Regex(
        @"@eaDir|\.DS_Store|\.qpkg|__MACOSX|@Recently-Snapshot|@recycle|\.@__thumb|\.caltrash|#recycle|\.yacreaderlibrary",
        MatchOptions, Parser.RegexTimeout);
    private static readonly Regex FileCopyAppend = new Regex(@"\(\d+\)",
        MatchOptions, Parser.RegexTimeout);
    public static readonly string BackupDirectory = Path.Join(Directory.GetCurrentDirectory(), "config", "backups");

    public DirectoryService(ILogger<DirectoryService> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        FileSystem = fileSystem;
        ConfigDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config");
        ExistOrCreate(ConfigDirectory);
        CoverImageDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "covers");
        ExistOrCreate(CoverImageDirectory);
        CacheDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "cache");
        ExistOrCreate(CacheDirectory);
        LogDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "logs");
        ExistOrCreate(LogDirectory);
        TempDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "temp");
        ExistOrCreate(TempDirectory);
        BookmarkDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "bookmarks");
        ExistOrCreate(BookmarkDirectory);
        AssetsDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "Assets");
        ExistOrCreate(AssetsDirectory);
        SiteThemeDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "themes");
        ExistOrCreate(SiteThemeDirectory);
        FaviconDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "favicons");
        ExistOrCreate(FaviconDirectory);
        LocalizationDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "I18N");
        CustomizedTemplateDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "templates");
        ExistOrCreate(CustomizedTemplateDirectory);
        TemplateDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "EmailTemplates");
        ExistOrCreate(TemplateDirectory);
        PublisherDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "images", "publishers");
        ExistOrCreate(PublisherDirectory);
        LongTermCacheDirectory = FileSystem.Path.Join(FileSystem.Directory.GetCurrentDirectory(), "config", "cache-long");
        ExistOrCreate(LongTermCacheDirectory);
    }

    /// <summary>
    /// Given a set of regex search criteria, get files in the given path.
    /// </summary>
    /// <remarks>This will always exclude <see cref="Tasks.Scanner.Parser.Parser.MacOsMetadataFileStartsWith"/> patterns</remarks>
    /// <param name="path">Directory to search</param>
    /// <param name="searchPatternExpression">Regex version of search pattern (e.g., \.mp3|\.mp4). Defaults to * meaning all files.</param>
    /// <param name="searchOption">SearchOption to use, defaults to TopDirectoryOnly</param>
    /// <returns>List of file paths</returns>
    public IEnumerable<string> GetFilesWithCertainExtensions(string path,
        string searchPatternExpression = "",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        // If directory doesn't exist, exit the iterator with no results
        if (!FileSystem.Directory.Exists(path))
            yield break;

        // Compile the regex pattern for faster repeated matching
        var reSearchPattern = new Regex(searchPatternExpression,
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            Parser.RegexTimeout);

        // Enumerate files in the directory and apply filters
        foreach (var file in FileSystem.Directory.EnumerateFiles(path, "*", searchOption))
        {
            var fileName = FileSystem.Path.GetFileName(file);
            var fileExtension = FileSystem.Path.GetExtension(file);

            // Check if the file matches the pattern and exclude macOS metadata files
            if (reSearchPattern.IsMatch(fileExtension) && !fileName.StartsWith(Parser.MacOsMetadataFileStartsWith))
            {
                yield return file;
            }
        }
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
            var folder = FileSystem.DirectoryInfo.New(path).Name;
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
        var di = FileSystem.DirectoryInfo.New(directory);
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
        if (!FileSystem.Directory.Exists(path))
            yield break; // Use yield break to exit the iterator early

        Regex? reSearchPattern = null;
        if (!string.IsNullOrEmpty(fileNameRegex))
        {
            // Compile the regex for better performance when used frequently
            reSearchPattern = new Regex(fileNameRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled, Tasks.Scanner.Parser.Parser.RegexTimeout);
        }

        // Enumerate files lazily
        foreach (var file in FileSystem.Directory.EnumerateFiles(path, "*", searchOption))
        {
            var fileName = FileSystem.Path.GetFileName(file);

            // Exclude macOS metadata files
            if (fileName.StartsWith(Tasks.Scanner.Parser.Parser.MacOsMetadataFileStartsWith))
                continue;

            // If a regex is provided, match the file name against it
            if (reSearchPattern != null && !reSearchPattern.IsMatch(fileName))
                continue;

            yield return file; // Yield each matching file as it's found
        }
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
            var fileInfo = FileSystem.FileInfo.New(fullFilePath);
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
    public bool CopyDirectoryToDirectory(string? sourceDirName, string destDirName, string searchPattern = "")
    {
        if (string.IsNullOrEmpty(sourceDirName)) return false;

        // Get the subdirectories for the specified directory.
        var dir = FileSystem.DirectoryInfo.New(sourceDirName);

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
        var files = GetFilesWithExtension(dir.FullName, searchPattern).Select(n => FileSystem.FileInfo.New(n));
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
        return FileSystem.DirectoryInfo.New(FileSystem.Path.GetPathRoot(path) ?? string.Empty).Exists;
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

        return !FileSystem.Directory.Exists(path) ? [] : FileSystem.Directory.GetFiles(path);
    }

    /// <summary>
    /// Returns the total number of bytes for a given set of full file paths
    /// </summary>
    /// <param name="paths"></param>
    /// <returns>Total bytes</returns>
    public long GetTotalSize(IEnumerable<string> paths)
    {
        return paths.Sum(path => FileSystem.FileInfo.New(path).Length);
    }

    /// <summary>
    /// Returns true if the path exists and is a directory. If path does not exist, this will create it. Returns false in all fail cases.
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <returns></returns>
    public bool ExistOrCreate(string directoryPath)
    {
        var di = FileSystem.DirectoryInfo.New(directoryPath);
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

        var di = FileSystem.DirectoryInfo.New(directoryPath);

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
        directoryPath = directoryPath.Replace(Environment.NewLine, string.Empty);
        var di = FileSystem.DirectoryInfo.New(directoryPath);
        if (!di.Exists) return;
        try
        {
            foreach (var file in di.EnumerateFiles())
            {
                if (!file.Exists) continue;
                file.Delete();
            }
            foreach (var dir in di.EnumerateDirectories())
            {
                if (!dir.Exists) continue;
                dir.Delete(true);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "[ClearDirectory] Could not delete {DirectoryPath} due to permission issue", directoryPath);
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
        string? currentFile = null;
        try
        {
            foreach (var file in filePaths)
            {
                currentFile = file;

                if (!FileSystem.File.Exists(file))
                {
                    _logger.LogError("Unable to copy {File} to {DirectoryPath} as it doesn't exist", file, directoryPath);
                    continue;
                }
                var fileInfo = FileSystem.FileInfo.New(file);
                var targetFile = FileSystem.FileInfo.New(RenameFileForCopy(file, directoryPath, prepend));

                fileInfo.CopyTo(FileSystem.Path.Join(directoryPath, targetFile.Name));
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
    /// Copies files to a destination directory. If the destination directory doesn't exist, this will create it.
    /// </summary>
    /// <remarks>If a file already exists in dest, this will rename as (2). It does not support multiple iterations of this. Overwriting is not supported.</remarks>
    /// <param name="filePaths"></param>
    /// <param name="directoryPath"></param>
    /// <param name="newFilenames">A list that matches one to one with filePaths. Each filepath will be renamed to newFilenames</param>
    /// <returns></returns>
    public bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath, IList<string> newFilenames)
    {
        ExistOrCreate(directoryPath);
        string? currentFile = null;
        var index = 0;
        try
        {
            foreach (var file in filePaths)
            {
                currentFile = file;

                if (!FileSystem.File.Exists(file))
                {
                    _logger.LogError("Unable to copy {File} to {DirectoryPath} as it doesn't exist", file, directoryPath);
                    continue;
                }
                var fileInfo = FileSystem.FileInfo.New(file);
                var targetFile = FileSystem.FileInfo.New(RenameFileForCopy(newFilenames[index] + fileInfo.Extension, directoryPath));

                fileInfo.CopyTo(FileSystem.Path.Join(directoryPath, targetFile.Name));
                index++;
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
    /// Generates the combined filepath given a prepend (optional), output directory path, and a full input file path.
    /// If the output file already exists, will append (1), (2), etc until it can be written out
    /// </summary>
    /// <param name="fileToCopy"></param>
    /// <param name="directoryPath"></param>
    /// <param name="prepend"></param>
    /// <returns></returns>
    private string RenameFileForCopy(string fileToCopy, string directoryPath, string prepend = "")
    {
        while (true)
        {
            var fileInfo = FileSystem.FileInfo.New(fileToCopy);
            var filename = prepend + fileInfo.Name;

            var targetFile = FileSystem.FileInfo.New(FileSystem.Path.Join(directoryPath, filename));
            if (!targetFile.Exists)
            {
                return targetFile.FullName;
            }

            var noExtension = FileSystem.Path.GetFileNameWithoutExtension(fileInfo.Name);
            if (FileCopyAppend.IsMatch(noExtension))
            {
                var match = FileCopyAppend.Match(noExtension).Value;
                var matchNumber = match.Replace("(", string.Empty).Replace(")", string.Empty);
                noExtension = noExtension.Replace(match, $"({int.Parse(matchNumber) + 1})");
            }
            else
            {
                noExtension += " (1)";
            }

            var newFilename = prepend + noExtension + FileSystem.Path.GetExtension(fileInfo.Name);
            fileToCopy = FileSystem.Path.Join(directoryPath, newFilename);
        }
    }

    /// <summary>
    /// Lists all directories in a root path. Will exclude Hidden or System directories.
    /// </summary>
    /// <param name="rootPath"></param>
    /// <returns></returns>
    public IEnumerable<DirectoryDto> ListDirectory(string rootPath)
    {
        if (!FileSystem.Directory.Exists(rootPath)) return ImmutableList<DirectoryDto>.Empty;

        var di = FileSystem.DirectoryInfo.New(rootPath);
        var dirs = di.GetDirectories()
            .Where(dir => !(dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System)))
            .Select(d => new DirectoryDto()
            {
                Name = d.Name,
                FullPath = d.FullName,
            })
            .OrderBy(s => s.Name)
            .ToImmutableList();

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
        foreach (var folder in libraryFolders.Select(Tasks.Scanner.Parser.Parser.NormalizePath))
        {
            if (stopLookingForDirectories) break;
            foreach (var file in filePaths.Select(Tasks.Scanner.Parser.Parser.NormalizePath))
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

                var fullPath = Tasks.Scanner.Parser.Parser.NormalizePath(Path.Join(folder, parts[parts.Count - 1]));
                dirs.TryAdd(fullPath, string.Empty);
            }
        }

        return dirs;
    }

    /// <summary>
    /// Finds the lowest directory from a set of file paths. Does not return the root path, will always select the lowest non-root path.
    /// </summary>
    /// <remarks>If the file paths do not contain anything from libraryFolders, this returns null.</remarks>
    /// <param name="libraryFolders">List of top level folders which files belong to</param>
    /// <param name="filePaths">List of file paths that belong to libraryFolders</param>
    /// <returns>Lowest non-root path, or null if not found</returns>
    public string? FindLowestDirectoriesFromFiles(IList<string> libraryFolders, IList<string> filePaths)
    {
        // Normalize the file paths only once
        var normalizedFilePaths = filePaths.Select(Parser.NormalizePath).ToList();

        // Use a list to store all directories for comparison
        var dirs = new List<string>();

        // Iterate through each library folder and collect matching directories
        foreach (var normalizedFolder in libraryFolders.Select(Parser.NormalizePath))
        {
            foreach (var file in normalizedFilePaths)
            {
                // If the file path contains the folder path, get its directory
                if (!file.Contains(normalizedFolder)) continue;

                var lowestPath = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(lowestPath))
                {
                    dirs.Add(Parser.NormalizePath(lowestPath)); // Add to list
                }
            }
        }

        if (dirs.Count == 0)
        {
            return null; // No directories found
        }

        // Now find the deepest common directory among all paths
        var commonPath = dirs.Aggregate(GetDeepestCommonPath); // Use new method to get deepest path

        // Return the common path if it exists and is not one of the root directories
        return libraryFolders.Any(folder => commonPath == Parser.NormalizePath(folder)) ? null : commonPath;
    }

    public static string GetDeepestCommonPath(string path1, string path2)
    {
        var parts1 = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts2 = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Get the longest matching parts, ensuring that deeper parts in hierarchy are considered
        var commonParts = parts1.Zip(parts2, (p1, p2) => p1 == p2 ? p1 : null)
            .TakeWhile(part => part != null)
            .ToArray();

        return Parser.NormalizePath(string.Join(Path.DirectorySeparatorChar.ToString(), commonParts));
    }


    /// <summary>
    /// Gets a set of directories from the folder path. Automatically excludes directories that shouldn't be in scope.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <returns>List of directory paths, empty if path doesn't exist</returns>
    public IEnumerable<string> GetDirectories(string folderPath)
    {
        if (!FileSystem.Directory.Exists(folderPath)) return ImmutableArray<string>.Empty;
        return FileSystem.Directory.GetDirectories(folderPath)
            .Where(path => ExcludeDirectories.Matches(path).Count == 0);
    }

    /// <summary>
    /// Gets a set of directories from the folder path. Automatically excludes directories that shouldn't be in scope.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="matcher">A set of glob rules that will filter directories out</param>
    /// <returns>List of directory paths, empty if path doesn't exist</returns>
    public IEnumerable<string> GetDirectories(string folderPath, GlobMatcher? matcher)
    {
        if (matcher == null) return GetDirectories(folderPath);

        return GetDirectories(folderPath)
            .Where(folder => !matcher.ExcludeMatches(
                $"{FileSystem.DirectoryInfo.New(folder).Name}{FileSystem.Path.AltDirectorySeparatorChar}"));
    }

    /// <summary>
    /// Returns all directories, including subdirectories. Automatically excludes directories that shouldn't be in scope.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="matcher"></param>
    /// <returns></returns>
    public IEnumerable<string> GetAllDirectories(string folderPath, GlobMatcher? matcher = null)
    {
        if (!FileSystem.Directory.Exists(folderPath)) return ImmutableArray<string>.Empty;
        var directories = new List<string>();

        var foundDirs = GetDirectories(folderPath, matcher);
        foreach (var foundDir in foundDirs)
        {
            directories.Add(foundDir);
            directories.AddRange(GetAllDirectories(foundDir, matcher));
        }

        return directories;
    }

    /// <summary>
    /// Returns the parent directories name for a file or folder. Empty string is path is not valid.
    /// </summary>
    /// <param name="fileOrFolder"></param>
    /// <returns></returns>
    public string GetParentDirectoryName(string fileOrFolder)
    {
        try
        {
            return Tasks.Scanner.Parser.Parser.NormalizePath(Directory.GetParent(fileOrFolder)?.FullName);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Scans a directory by utilizing a recursive folder search.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="fileTypes"></param>
    /// <param name="matcher"></param>
    /// <param name="searchOption">Pass TopDirectories</param>
    /// <returns></returns>
    public IList<string> ScanFiles(string folderPath, string fileTypes, GlobMatcher? matcher = null,
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        var files = new List<string>();

        if (!Exists(folderPath)) return files;

        if (searchOption == SearchOption.AllDirectories)
        {

            // Stack to hold directories to process
            var directoriesToProcess = new Stack<string>();
            directoriesToProcess.Push(folderPath);

            while (directoriesToProcess.Count > 0)
            {
                var currentDirectory = directoriesToProcess.Pop();

                // Get files from the current directory
                var filesInCurrentDirectory = GetFilesWithCertainExtensions(currentDirectory, fileTypes);
                files.AddRange(filesInCurrentDirectory);

                // Get subdirectories and add them to the stack
                var subdirectories = GetDirectories(currentDirectory, matcher);
                foreach (var subdirectory in subdirectories)
                {
                    directoriesToProcess.Push(subdirectory);
                }
            }
        }
        else
        {
            // If TopDirectoryOnly is specified, only get files in the specified folder
            var filesInCurrentDirectory = GetFilesWithCertainExtensions(folderPath, fileTypes);
            files.AddRange(filesInCurrentDirectory);
        }

        // Filter out unwanted files based on matcher if provided
        if (matcher != null)
        {
            files = files.Where(file => !matcher.ExcludeMatches(FileSystem.FileInfo.New(file).Name)).ToList();
        }

        return files;
    }


    /// <summary>
    /// Recursively scans a folder and returns the max last write time on any folders and files
    /// </summary>
    /// <remarks>If the folder is empty or non-existent, this will return MaxValue for a DateTime</remarks>
    /// <param name="folderPath"></param>
    /// <returns>Max Last Write Time</returns>
    public DateTime GetLastWriteTime(string folderPath)
    {
        if (!FileSystem.Directory.Exists(folderPath)) return DateTime.MaxValue;

        var fileEntries = FileSystem.Directory.GetFileSystemEntries(folderPath, "*.*", SearchOption.AllDirectories);
        if (fileEntries.Length == 0) return DateTime.MaxValue;

        // Find the max last write time of the files
        var maxFiles = fileEntries.Max(path => FileSystem.File.GetLastWriteTime(path));

        // Get the last write time of the directory itself
        var directoryLastWriteTime = FileSystem.Directory.GetLastWriteTime(folderPath);

        // Use comparison to get the max DateTime value
        return directoryLastWriteTime > maxFiles ? directoryLastWriteTime : maxFiles;
    }


    /// <summary>
    /// Recursively scans files and applies an action on them. This uses as many cores the underlying PC has to speed
    /// up processing.
    /// NOTE: This is no longer parallel due to user's machines locking up
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
                subDirs = GetDirectories(currentDir);
            }
            // Thrown if we do not have discovery permission on the directory.
            catch (UnauthorizedAccessException e) {
                logger.LogCritical(e, "Unauthorized access on {Directory}", currentDir);
                continue;
            }
            // Thrown if another process has deleted the directory after we retrieved its name.
            catch (DirectoryNotFoundException e) {
                logger.LogCritical(e, "Directory not found on {Directory}", currentDir);
                continue;
            }

            try {
                files = GetFilesWithCertainExtensions(currentDir, searchPattern)
                    .ToArray();
            }
            catch (UnauthorizedAccessException e) {
                logger.LogCritical(e, "Unauthorized access on a file in {Directory}", currentDir);
                continue;
            }
            catch (DirectoryNotFoundException e) {
                logger.LogCritical(e, "Directory not found on a file in {Directory}", currentDir);
                continue;
            }
            catch (IOException e) {
                logger.LogCritical(e, "IO exception on a file in {Directory}", currentDir);
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
                    if (ex is not UnauthorizedAccessException) return false;
                    // Here we just output a message and go on.
                    _logger.LogError(ex, "Unauthorized access on file");
                    return true;
                    // Handle other exceptions here if necessary...

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
                FileSystem.FileInfo.New(file).Delete();
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
        DeleteFiles(GetFiles(directoryName, searchOption:SearchOption.AllDirectories).Where(file => !Tasks.Scanner.Parser.Parser.IsImage(file)));
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

        var directory = FileSystem.DirectoryInfo.New(directoryName);

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
        catch (Exception)
        {
            ClearAndDeleteDirectory(directoryName);
            return false;
        }

        ClearAndDeleteDirectory(directoryName);
        return true;
    }


    private static void FlattenDirectory(IFileSystemInfo root, IDirectoryInfo directory, ref int directoryIndex)
    {
        if (!root.FullName.Equals(directory.FullName))
        {
            var fileIndex = 1;

            foreach (var file in directory.EnumerateFiles().OrderByNatural(file => file.FullName))
            {
                if (file.Directory == null) continue;
                var paddedIndex = Tasks.Scanner.Parser.Parser.PadZeros(directoryIndex + string.Empty);
                // We need to rename the files so that after flattening, they are in the order we found them
                var newName = $"{paddedIndex}_{Tasks.Scanner.Parser.Parser.PadZeros(fileIndex + string.Empty)}{file.Extension}";
                var newPath = Path.Join(root.FullName, newName);
                if (!File.Exists(newPath)) file.MoveTo(newPath);
                fileIndex++;
            }

            directoryIndex++;
        }

        foreach (var subDirectory in directory.EnumerateDirectories().OrderByNatural(d => d.FullName))
        {
            // We need to check if the directory is not a blacklisted (ie __MACOSX)
            if (Tasks.Scanner.Parser.Parser.HasBlacklistedFolderInPath(subDirectory.FullName)) continue;

            FlattenDirectory(root, subDirectory, ref directoryIndex);
        }
    }
}
