using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{
    /// <summary>
    /// Responsible for manipulating Archive files. Used by <see cref="CacheService"/> and <see cref="ScannerService"/>
    /// </summary>
    public class ArchiveService : IArchiveService
    {
        private readonly ILogger<ArchiveService> _logger;
        private const int ThumbnailWidth = 320;

        public ArchiveService(ILogger<ArchiveService> logger)
        {
            _logger = logger;
        }
        
        public int GetNumberOfPagesFromArchive(string archivePath)
        {
            if (!IsValidArchive(archivePath)) return 0;

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(archivePath);
                return archive.Entries.Count(e => Parser.Parser.IsImage(e.FullName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                return 0;
            }
        }
        
        /// <summary>
        /// Generates byte array of cover image.
        /// Given a path to a compressed file (zip, rar, cbz, cbr, etc), will ensure the first image is returned unless
        /// a folder.extension exists in the root directory of the compressed file.
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="createThumbnail">Create a smaller variant of file extracted from archive. Archive images are usually 1MB each.</param>
        /// <returns></returns>
        public byte[] GetCoverImage(string filepath, bool createThumbnail = false)
        {
            try
            {
                if (!IsValidArchive(filepath)) return Array.Empty<byte>();

                using var archive = ZipFile.OpenRead(filepath);
                if (!archive.HasFiles()) return Array.Empty<byte>();

                var folder = archive.Entries.SingleOrDefault(x => Path.GetFileNameWithoutExtension(x.Name).ToLower() == "folder");
                var entries = archive.Entries.Where(x => Path.HasExtension(x.FullName) && Parser.Parser.IsImage(x.FullName)).OrderBy(x => x.FullName).ToList();
                var entry = folder ?? entries[0];

                return createThumbnail ? CreateThumbnail(entry) : ConvertEntryToByteArray(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when reading archive stream: {Filepath}. Defaulting to no cover image", filepath);
            }
            
            return Array.Empty<byte>();
        }

        private byte[] CreateThumbnail(ZipArchiveEntry entry)
        {
            try
            {
                using var stream = entry.Open();
                using var thumbnail = Image.ThumbnailStream(stream, ThumbnailWidth);
                return thumbnail.WriteToBuffer(".jpg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was a critical error and prevented thumbnail generation on {EntryName}. Defaulting to no cover image", entry.FullName);
            }

            return Array.Empty<byte>();
        }

        private static byte[] ConvertEntryToByteArray(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            return data;
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
                archive.Entries.Any(e => e.FullName.Contains(Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Test if the archive path exists and there are images inside it. This will log as an error. 
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        public bool IsValidArchive(string archivePath)
        {
            try
            {
                if (!File.Exists(archivePath))
                {
                    _logger.LogError("Archive {ArchivePath} could not be found", archivePath);
                    return false;
                }

                if (!Parser.Parser.IsArchive(archivePath))
                {
                    _logger.LogError("Archive {ArchivePath} is not a valid archive", archivePath);
                    return false;
                }

                using var archive = ZipFile.OpenRead(archivePath);
                if (archive.Entries.Any(e => Parser.Parser.IsImage(e.FullName))) return true;
                _logger.LogError("Archive {ArchivePath} contains no images", archivePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to validate archive ({ArchivePath}) due to problem opening archive", archivePath);
            }
            return false;

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

            if (Directory.Exists(extractPath))
            {
                _logger.LogDebug("Archive {ArchivePath} has already been extracted. Returning existing folder", archivePath);
                return;
            }
           
            Stopwatch sw = Stopwatch.StartNew();
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            var needsFlattening = ArchiveNeedsFlattening(archive);
            if (!archive.HasFiles() && !needsFlattening) return;
            
            archive.ExtractToDirectory(extractPath, true);
            _logger.LogDebug("Extracted archive to {ExtractPath} in {ElapsedMilliseconds} milliseconds", extractPath, sw.ElapsedMilliseconds);

            if (needsFlattening)
            {
                sw = Stopwatch.StartNew();
                _logger.LogInformation("Extracted archive is nested in root folder, flattening...");
                new DirectoryInfo(extractPath).Flatten();
                _logger.LogInformation("Flattened in {ElapsedMilliseconds} milliseconds", sw.ElapsedMilliseconds);
            }
        }
    }
}