using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;
using API.Archive;
using API.Comparators;
using API.Extensions;
using API.Interfaces.Services;
using API.Services.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using SharpCompress.Archives;
using SharpCompress.Common;
using Image = NetVips.Image;

namespace API.Services
{
    /// <summary>
    /// Responsible for manipulating Archive files. Used by <see cref="CacheService"/> and <see cref="ScannerService"/>
    /// </summary>
    public class ArchiveService : IArchiveService
    {
        private readonly ILogger<ArchiveService> _logger;
        private const int ThumbnailWidth = 320; // 153w x 230h
        private static readonly RecyclableMemoryStreamManager _streamManager = new();
        private readonly NaturalSortComparer _comparer;

        public ArchiveService(ILogger<ArchiveService> logger)
        {
            _logger = logger;
            _comparer = new NaturalSortComparer();
        }
        
        /// <summary>
        /// Checks if a File can be opened. Requires up to 2 opens of the filestream.
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        public ArchiveLibrary CanOpen(string archivePath)
        {
            if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath)) return ArchiveLibrary.NotSupported;
            
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
                        _logger.LogDebug("Using default compression handling");
                        using ZipArchive archive = ZipFile.OpenRead(archivePath);
                        return archive.Entries.Count(e => !Parser.Parser.HasBlacklistedFolderInPath(e.FullName) && Parser.Parser.IsImage(e.FullName));
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        return archive.Entries.Count(entry => !entry.IsDirectory && 
                                                              !Parser.Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                              && Parser.Parser.IsImage(entry.Key));
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[GetNumberOfPagesFromArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return 0;
                    default:
                        _logger.LogError("[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                return 0;
            }
        }
        
        /// <summary>
        /// Generates byte array of cover image.
        /// Given a path to a compressed file (zip, rar, cbz, cbr, etc), will ensure the first image is returned unless
        /// a folder.extension exists in the root directory of the compressed file.
        /// </summary>
        /// <param name="archivePath"></param>
        /// <param name="createThumbnail">Create a smaller variant of file extracted from archive. Archive images are usually 1MB each.</param>
        /// <returns></returns>
        public byte[] GetCoverImage(string archivePath, bool createThumbnail = false)
        {
            if (archivePath == null || !IsValidArchive(archivePath)) return Array.Empty<byte>();
            try
            {
                var libraryHandler = CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        _logger.LogDebug("Using default compression handling");
                        using var archive = ZipFile.OpenRead(archivePath);
                        // NOTE: We can probably reduce our iteration by performing 1 filter on MACOSX then do our folder check and image chack. 
                        var folder = archive.Entries.SingleOrDefault(x => !Parser.Parser.HasBlacklistedFolderInPath(x.FullName) 
                                                                          && Parser.Parser.IsImage(x.FullName)
                                                                          && Parser.Parser.IsCoverImage(x.FullName));
                        var entries = archive.Entries.Where(x => Path.HasExtension(x.FullName) 
                                                                 && !Parser.Parser.HasBlacklistedFolderInPath(x.FullName)
                                                                 && Parser.Parser.IsImage(x.FullName))
                                                                .OrderBy(x => x.FullName, _comparer).ToList();
                        var entry = folder ?? entries[0];

                        return createThumbnail ? CreateThumbnail(entry) : ConvertEntryToByteArray(entry);
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        var entries = archive.Entries
                                                                .Where(entry => !entry.IsDirectory
                                                                    && !Parser.Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                                     && Parser.Parser.IsImage(entry.Key))
                                                                .OrderBy(x => x.Key, _comparer);
                        return FindCoverImage(entries, createThumbnail);
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[GetCoverImage] This archive cannot be read: {ArchivePath}. Defaulting to no cover image", archivePath);
                        return Array.Empty<byte>();
                    default:
                        _logger.LogError("[GetCoverImage] There was an exception when reading archive stream: {ArchivePath}. Defaulting to no cover image", archivePath);
                        return Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetCoverImage] There was an exception when reading archive stream: {ArchivePath}. Defaulting to no cover image", archivePath);
            }
            
            return Array.Empty<byte>();
        }

        private byte[] FindCoverImage(IEnumerable<IArchiveEntry> entries, bool createThumbnail)
        {
            var images = entries.ToList();
            foreach (var entry in images)
            {
                if (Path.GetFileNameWithoutExtension(entry.Key).ToLower() == "folder")
                {
                    using var ms = _streamManager.GetStream();
                    entry.WriteTo(ms);
                    ms.Position = 0;
                    var data = ms.ToArray();
                    return createThumbnail ? CreateThumbnail(data, Path.GetExtension(entry.Key)) : data;
                }
            }

            if (images.Any())
            {
                var entry = images.OrderBy(e => e.Key).FirstOrDefault();
                if (entry == null) return Array.Empty<byte>();
                using var ms = _streamManager.GetStream();
                entry.WriteTo(ms);
                ms.Position = 0;
                var data = ms.ToArray();
                return createThumbnail ? CreateThumbnail(data, Path.GetExtension(entry.Key)) : data;
            }
            
            return Array.Empty<byte>();
        }
        
        private static byte[] ConvertEntryToByteArray(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var ms = _streamManager.GetStream();
            stream.CopyTo(ms);
            return ms.ToArray();
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

        private byte[] CreateThumbnail(byte[] entry, string formatExtension = ".jpg")
        {
            if (!formatExtension.StartsWith("."))
            {
                formatExtension = "." + formatExtension;
            }
            
            try
            {
                using var thumbnail = Image.ThumbnailBuffer(entry, ThumbnailWidth);
                return thumbnail.WriteToBuffer(formatExtension); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CreateThumbnail] There was a critical error and prevented thumbnail generation. Defaulting to no cover image. Format Extension {Extension}", formatExtension);
            }

            return Array.Empty<byte>();
        }
        
        private byte[] CreateThumbnail(ZipArchiveEntry entry, string formatExtension = ".jpg")
        {
            if (!formatExtension.StartsWith("."))
            {
                formatExtension = $".{formatExtension}";
            }
            try
            {
                using var stream = entry.Open();
                using var thumbnail = Image.ThumbnailStream(stream, ThumbnailWidth);
                return thumbnail.WriteToBuffer(formatExtension);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was a critical error and prevented thumbnail generation on {EntryName}. Defaulting to no cover image", entry.FullName);
            }

            return Array.Empty<byte>();
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
                _logger.LogError("Archive {ArchivePath} could not be found", archivePath);
                return false;
            }

            if (Parser.Parser.IsArchive(archivePath)) return true;
            
            _logger.LogError("Archive {ArchivePath} is not a valid archive", archivePath);
            return false;
        }

        
        private static ComicInfo FindComicInfoXml(IEnumerable<IArchiveEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (Path.GetFileNameWithoutExtension(entry.Key).ToLower().EndsWith("comicinfo") && !Parser.Parser.HasBlacklistedFolderInPath(entry.Key) && Parser.Parser.IsXml(entry.Key))
                {
                    using var ms = _streamManager.GetStream();
                    entry.WriteTo(ms);
                    ms.Position = 0;


                    var serializer = new XmlSerializer(typeof(ComicInfo));
                    var info = (ComicInfo) serializer.Deserialize(ms);
                    return info;
                }
            }

            
            return null;
        }

        public string GetSummaryInfo(string archivePath)
        {
            var summary = string.Empty;
            if (!IsValidArchive(archivePath)) return summary;

            ComicInfo info = null;
            try
            {
                if (!File.Exists(archivePath)) return summary;
                
                var libraryHandler = CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        _logger.LogDebug("Using default compression handling");
                        using var archive = ZipFile.OpenRead(archivePath);
                        var entry = archive.Entries.SingleOrDefault(x => !Parser.Parser.HasBlacklistedFolderInPath(x.FullName) &&  Path.GetFileNameWithoutExtension(x.Name).ToLower() == "comicinfo" && Parser.Parser.IsXml(x.FullName));
                        if (entry != null)
                        {
                            using var stream = entry.Open();
                            var serializer = new XmlSerializer(typeof(ComicInfo));
                            info = (ComicInfo) serializer.Deserialize(stream);
                        }
                        break;
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        info = FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory 
                                                                               && !Parser.Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                                               && Parser.Parser.IsXml(entry.Key)));
                        break;
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[GetSummaryInfo] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return summary;
                    default:
                        _logger.LogError("[GetSummaryInfo] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return summary;
                }

                if (info != null)
                {
                    return info.Summary;
                }

                _logger.LogError("[GetSummaryInfo] Could not parse archive file: {Filepath}", archivePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetSummaryInfo] There was an exception when reading archive stream: {Filepath}", archivePath);
            }
            
            return summary;
        }

        private static void ExtractArchiveEntities(IEnumerable<IArchiveEntry> entries, string extractPath)
        {
            DirectoryService.ExistOrCreate(extractPath);
            foreach (var entry in entries)
            {
                entry.WriteToDirectory(extractPath, new ExtractionOptions()
                {
                    ExtractFullPath = false,
                    Overwrite = false
                });
            }
        }

        private void ExtractArchiveEntries(ZipArchive archive, string extractPath)
        {
            var needsFlattening = ArchiveNeedsFlattening(archive);
            if (!archive.HasFiles() && !needsFlattening) return;
            
            archive.ExtractToDirectory(extractPath, true);
            if (needsFlattening)
            {
                _logger.LogDebug("Extracted archive is nested in root folder, flattening...");
                new DirectoryInfo(extractPath).Flatten();
            }
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
                        _logger.LogDebug("Using default compression handling");
                        using var archive = ZipFile.OpenRead(archivePath);
                        ExtractArchiveEntries(archive, extractPath);
                        break;
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        ExtractArchiveEntities(archive.Entries.Where(entry => !entry.IsDirectory 
                                                                              && !Parser.Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                                              && Parser.Parser.IsImage(entry.Key)), extractPath);
                        break;
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[GetNumberOfPagesFromArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                    default:
                        _logger.LogError("[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                }
                
            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was a problem extracting {ArchivePath} to {ExtractPath}",archivePath, extractPath);
                return;
            }
            _logger.LogDebug("Extracted archive to {ExtractPath} in {ElapsedMilliseconds} milliseconds", extractPath, sw.ElapsedMilliseconds);
        }
    }
}