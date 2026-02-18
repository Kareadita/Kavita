using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.System;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace Kavita.API.Services;

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
    string EpubFontDirectory { get; }
    string BackupDirectory { get; }

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
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = true);
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
