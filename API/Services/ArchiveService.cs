using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.Archive;
using API.Comparators;
using API.Data.Metadata;
using API.Extensions;
using API.Interfaces.Services;
using API.Services.Tasks;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace API.Services
{
    /// <summary>
    /// Responsible for manipulating Archive files. Used by <see cref="CacheService"/> and <see cref="ScannerService"/>
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ArchiveService : IArchiveService
    {
        private readonly ILogger<ArchiveService> _logger;
        private readonly IDirectoryService _directoryService;
        private const string ComicInfoFilename = "comicinfo";

        public ArchiveService(ILogger<ArchiveService> logger, IDirectoryService directoryService)
        {
            _logger = logger;
            _directoryService = directoryService;
        }

        /// <summary>
        /// Checks if a File can be opened. Requires up to 2 opens of the filestream.
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        public virtual ArchiveLibrary CanOpen(string archivePath)
        {
            if (!(File.Exists(archivePath) && Parser.Parser.IsArchive(archivePath) || Parser.Parser.IsEpub(archivePath))) return ArchiveLibrary.NotSupported;

            try
            {
                using var a2 = ZipFile.OpenRead(archivePath);
                return ArchiveLibrary.Default;
            }
            catch (Exception)
            {
                try
                {
                    using var a1 = ArchiveFactory.Open(archivePath);
                    return ArchiveLibrary.SharpCompress;
                }
                catch (Exception)
                {
                    return ArchiveLibrary.NotSupported;
                }
            }
        }

        public int GetNumberOfPagesFromArchive(string archivePath)
        {
            if (!IsValidArchive(archivePath))
            {
                _logger.LogError("Archive {ArchivePath} could not be found", archivePath);
                return 0;
            }

            try
            {
                var libraryHandler = CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        using var archive = ZipFile.OpenRead(archivePath);
                        return archive.Entries.Count(e => !Parser.Parser.HasBlacklistedFolderInPath(e.FullName) && Parser.Parser.IsImage(e.FullName));
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        using var archive = ArchiveFactory.Open(archivePath);
                        return archive.Entries.Count(entry => !entry.IsDirectory &&
                                                              !Parser.Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                              && Parser.Parser.IsImage(entry.Key));
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogWarning("[GetNumberOfPagesFromArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return 0;
                    default:
                        _logger.LogWarning("[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                return 0;
            }
        }

        /// <summary>
        /// Finds the first instance of a folder entry and returns it
        /// </summary>
        /// <param name="entryFullNames"></param>
        /// <returns>Entry name of match, null if no match</returns>
        public string FindFolderEntry(IEnumerable<string> entryFullNames)
        {
            var result = entryFullNames
                .FirstOrDefault(x => !Path.EndsInDirectorySeparator(x) && !Parser.Parser.HasBlacklistedFolderInPath(x)
                       && Parser.Parser.IsCoverImage(x)
                       && !x.StartsWith(Parser.Parser.MacOsMetadataFileStartsWith));

            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// Returns first entry that is an image and is not in a blacklisted folder path. Uses <see cref="NaturalSortComparer"/> for ordering files
        /// </summary>
        /// <param name="entryFullNames"></param>
        /// <returns>Entry name of match, null if no match</returns>
        public static string FirstFileEntry(IEnumerable<string> entryFullNames, string archiveName)
        {
            // First check if there are any files that are not in a nested folder before just comparing by filename. This is needed
            // because NaturalSortComparer does not work with paths and doesn't seem 001.jpg as before chapter 1/001.jpg.
            var fullNames = entryFullNames.Where(x =>!Parser.Parser.HasBlacklistedFolderInPath(x)
                                                     && Parser.Parser.IsImage(x)
                                                     && !x.StartsWith(Parser.Parser.MacOsMetadataFileStartsWith)).ToList();
            if (fullNames.Count == 0) return null;

            var nonNestedFile = fullNames.Where(entry => (Path.GetDirectoryName(entry) ?? string.Empty).Equals(archiveName))
                .OrderBy(Path.GetFullPath, new NaturalSortComparer())
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(nonNestedFile)) return nonNestedFile;

            // Check the first folder and sort within that to see if we can find a file, else fallback to first file with basic sort.
            // Get first folder, then sort within that
            var firstDirectoryFile = fullNames.OrderBy(Path.GetDirectoryName, new NaturalSortComparer()).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstDirectoryFile))
            {
                var firstDirectory = Path.GetDirectoryName(firstDirectoryFile);
                if (!string.IsNullOrEmpty(firstDirectory))
                {
                    var firstDirectoryResult = fullNames.Where(f => firstDirectory.Equals(Path.GetDirectoryName(f)))
                        .OrderBy(Path.GetFileName, new NaturalSortComparer())
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(firstDirectoryResult)) return firstDirectoryResult;
                }
            }

            var result = fullNames
                .OrderBy(Path.GetFileName, new NaturalSortComparer())
                .FirstOrDefault();

            return string.IsNullOrEmpty(result) ? null : result;
        }


        /// <summary>
        /// Generates byte array of cover image.
        /// Given a path to a compressed file <see cref="Parser.Parser.ArchiveFileExtensions"/>, will ensure the first image (respects directory structure) is returned unless
        /// a folder/cover.(image extension) exists in the the compressed file (if duplicate, the first is chosen)
        ///
        /// This skips over any __MACOSX folder/file iteration.
        /// </summary>
        /// <remarks>This always creates a thumbnail</remarks>
        /// <param name="archivePath"></param>
        /// <param name="fileName">File name to use based on context of entity.</param>
        /// <returns></returns>
        public string GetCoverImage(string archivePath, string fileName)
        {
            if (archivePath == null || !IsValidArchive(archivePath)) return string.Empty;
            try
            {
                var libraryHandler = CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        using var archive = ZipFile.OpenRead(archivePath);
                        var entryNames = archive.Entries.Select(e => e.FullName).ToArray();

                        var entryName = FindFolderEntry(entryNames) ?? FirstFileEntry(entryNames, Path.GetFileName(archivePath));
                        var entry = archive.Entries.Single(e => e.FullName == entryName);
                        using var stream = entry.Open();

                        return CreateThumbnail(archivePath + " - " + entry.FullName, stream, fileName);
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        using var archive = ArchiveFactory.Open(archivePath);
                        var entryNames = archive.Entries.Where(archiveEntry => !archiveEntry.IsDirectory).Select(e => e.Key).ToList();

                        var entryName = FindFolderEntry(entryNames) ?? FirstFileEntry(entryNames, Path.GetFileName(archivePath));
                        var entry = archive.Entries.Single(e => e.Key == entryName);

                        using var stream = entry.OpenEntryStream();

                        return CreateThumbnail(archivePath + " - " + entry.Key, stream, fileName);
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogWarning("[GetCoverImage] This archive cannot be read: {ArchivePath}. Defaulting to no cover image", archivePath);
                        return string.Empty;
                    default:
                        _logger.LogWarning("[GetCoverImage] There was an exception when reading archive stream: {ArchivePath}. Defaulting to no cover image", archivePath);
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetCoverImage] There was an exception when reading archive stream: {ArchivePath}. Defaulting to no cover image", archivePath);
            }

            return string.Empty;
        }

        /// <summary>
        /// Given an archive stream, will assess whether directory needs to be flattened so that the extracted archive files are directly
        /// under extract path and not nested in subfolders. See <see cref="DirectoryInfoExtensions"/> Flatten method.
        /// </summary>
        /// <param name="archive">An opened archive stream</param>
        /// <returns></returns>
        public bool ArchiveNeedsFlattening(ZipArchive archive)
        {
            // Sometimes ZipArchive will list the directory and others it will just keep it in the FullName
            return archive.Entries.Count > 0 &&
                   !Path.HasExtension(archive.Entries.ElementAt(0).FullName) ||
                   archive.Entries.Any(e => e.FullName.Contains(Path.AltDirectorySeparatorChar) && !Parser.Parser.HasBlacklistedFolderInPath(e.FullName));
        }

        // TODO: Refactor CreateZipForDownload to return the temp file so we can stream it from temp
        public async Task<Tuple<byte[], string>> CreateZipForDownload(IEnumerable<string> files, string tempFolder)
        {
            var dateString = DateTime.Now.ToShortDateString().Replace("/", "_");

            var tempLocation = Path.Join(DirectoryService.TempDirectory, $"{tempFolder}_{dateString}");
            DirectoryService.ExistOrCreate(tempLocation);
            if (!_directoryService.CopyFilesToDirectory(files, tempLocation))
            {
                throw new KavitaException("Unable to copy files to temp directory archive download.");
            }

            var zipPath = Path.Join(DirectoryService.TempDirectory, $"kavita_{tempFolder}_{dateString}.zip");
            try
            {
                ZipFile.CreateFromDirectory(tempLocation, zipPath);
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "There was an issue creating temp archive");
                throw new KavitaException("There was an issue creating temp archive");
            }


            var fileBytes = await _directoryService.ReadFileAsync(zipPath);

            DirectoryService.ClearAndDeleteDirectory(tempLocation);
            (new FileInfo(zipPath)).Delete();

            return Tuple.Create(fileBytes, zipPath);
        }

        private string CreateThumbnail(string entryName, Stream stream, string fileName)
        {
            try
            {
                return ImageService.WriteCoverThumbnail(stream, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {EntryName}. Defaulting to no cover image", entryName);
            }

            return string.Empty;
        }

        /// <summary>
        /// Test if the archive path exists and an archive
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        public bool IsValidArchive(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                _logger.LogWarning("Archive {ArchivePath} could not be found", archivePath);
                return false;
            }

            if (Parser.Parser.IsArchive(archivePath) || Parser.Parser.IsEpub(archivePath)) return true;

            _logger.LogWarning("Archive {ArchivePath} is not a valid archive", archivePath);
            return false;
        }


        private static ComicInfo FindComicInfoXml(IEnumerable<IArchiveEntry> entries)
        {
            foreach (var entry in entries)
            {
                var filename = Path.GetFileNameWithoutExtension(entry.Key).ToLower();
                if (filename.EndsWith(ComicInfoFilename)
                    && !filename.StartsWith(Parser.Parser.MacOsMetadataFileStartsWith)
                    && !Parser.Parser.HasBlacklistedFolderInPath(entry.Key)
                    && Parser.Parser.IsXml(entry.Key))
                {
                    using var ms = entry.OpenEntryStream();

                    var serializer = new XmlSerializer(typeof(ComicInfo));
                    var info = (ComicInfo) serializer.Deserialize(ms);
                    return info;
                }
            }


            return null;
        }

        public ComicInfo GetComicInfo(string archivePath)
        {
            if (!IsValidArchive(archivePath)) return null;

            try
            {
                if (!File.Exists(archivePath)) return null;

                var libraryHandler = CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        using var archive = ZipFile.OpenRead(archivePath);
                        var entry = archive.Entries.SingleOrDefault(x =>
                            !Parser.Parser.HasBlacklistedFolderInPath(x.FullName)
                            && Path.GetFileNameWithoutExtension(x.Name)?.ToLower() == ComicInfoFilename
                            && !Path.GetFileNameWithoutExtension(x.Name)
                                .StartsWith(Parser.Parser.MacOsMetadataFileStartsWith)
                            && Parser.Parser.IsXml(x.FullName));
                        if (entry != null)
                        {
                            using var stream = entry.Open();
                            var serializer = new XmlSerializer(typeof(ComicInfo));
                            return (ComicInfo) serializer.Deserialize(stream);
                        }

                        break;
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        using var archive = ArchiveFactory.Open(archivePath);
                        return FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory
                                                                               && !Parser.Parser
                                                                                   .HasBlacklistedFolderInPath(
                                                                                       Path.GetDirectoryName(
                                                                                           entry.Key) ?? string.Empty)
                                                                               && !Path
                                                                                   .GetFileNameWithoutExtension(
                                                                                       entry.Key).StartsWith(Parser
                                                                                       .Parser
                                                                                       .MacOsMetadataFileStartsWith)
                                                                               && Parser.Parser.IsXml(entry.Key)));
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogWarning("[GetComicInfo] This archive cannot be read: {ArchivePath}", archivePath);
                        return null;
                    default:
                        _logger.LogWarning(
                            "[GetComicInfo] There was an exception when reading archive stream: {ArchivePath}",
                            archivePath);
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetComicInfo] There was an exception when reading archive stream: {Filepath}", archivePath);
            }

            return null;
        }


        private static void ExtractArchiveEntities(IEnumerable<IArchiveEntry> entries, string extractPath)
        {
            DirectoryService.ExistOrCreate(extractPath);
            foreach (var entry in entries)
            {
                entry.WriteToDirectory(extractPath, new ExtractionOptions()
                {
                    ExtractFullPath = true, // Don't flatten, let the flatterner ensure correct order of nested folders
                    Overwrite = false
                });
            }
        }

        private void ExtractArchiveEntries(ZipArchive archive, string extractPath)
        {
            // NOTE: In cases where we try to extract, but there are InvalidPathChars, we need to inform the user
            var needsFlattening = ArchiveNeedsFlattening(archive);
            if (!archive.HasFiles() && !needsFlattening) return;

            archive.ExtractToDirectory(extractPath, true);
            if (!needsFlattening) return;

            _logger.LogDebug("Extracted archive is nested in root folder, flattening...");
            new DirectoryInfo(extractPath).Flatten();
        }

        /// <summary>
        /// Extracts an archive to a temp cache directory. Returns path to new directory. If temp cache directory already exists,
        /// will return that without performing an extraction. Returns empty string if there are any invalidations which would
        /// prevent operations to perform correctly (missing archivePath file, empty archive, etc).
        /// </summary>
        /// <param name="archivePath">A valid file to an archive file.</param>
        /// <param name="extractPath">Path to extract to</param>
        /// <returns></returns>
        public void ExtractArchive(string archivePath, string extractPath)
        {
            if (!IsValidArchive(archivePath)) return;

            if (Directory.Exists(extractPath)) return;

            var sw = Stopwatch.StartNew();

            try
            {
                var libraryHandler = CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        using var archive = ZipFile.OpenRead(archivePath);
                        ExtractArchiveEntries(archive, extractPath);
                        break;
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        using var archive = ArchiveFactory.Open(archivePath);
                        ExtractArchiveEntities(archive.Entries.Where(entry => !entry.IsDirectory
                                                                              && !Parser.Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                                              && Parser.Parser.IsImage(entry.Key)), extractPath);
                        break;
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogWarning("[ExtractArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                    default:
                        _logger.LogWarning("[ExtractArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                }

            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "[ExtractArchive] There was a problem extracting {ArchivePath} to {ExtractPath}",archivePath, extractPath);
                return;
            }
            _logger.LogDebug("Extracted archive to {ExtractPath} in {ElapsedMilliseconds} milliseconds", extractPath, sw.ElapsedMilliseconds);
        }
    }
}
