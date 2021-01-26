using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using API.Extensions;
using API.Interfaces;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    /// <summary>
    /// Responsible for manipulating Archive files. Used by <see cref="CacheService"/> almost exclusively.
    /// </summary>
    public class ArchiveService : IArchiveService
    {
        private readonly ILogger<ArchiveService> _logger;

        public ArchiveService(ILogger<ArchiveService> logger)
        {
            _logger = logger;
        }

        public bool ArchiveNeedsFlattening(ZipArchive archive)
        {
            // Sometimes ZipArchive will list the directory and others it will just keep it in the FullName
            return archive.Entries.Count > 0 &&
                !Path.HasExtension(archive.Entries.ElementAt(0).FullName) ||
                archive.Entries.Any(e => e.FullName.Contains(Path.AltDirectorySeparatorChar));
            
            // return archive.Entries.Count > 0 &&
            //        archive.Entries.Any(e => e.FullName.Contains(Path.AltDirectorySeparatorChar));
            //return archive.Entries.Count > 0 && !Path.HasExtension(archive.Entries.ElementAt(0).FullName);
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
            if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath))
            {
                _logger.LogError($"Archive {archivePath} could not be found.");
                return;
            }

            if (Directory.Exists(extractPath))
            {
                _logger.LogDebug($"Archive {archivePath} has already been extracted. Returning existing folder.");
                return;
            }
           
            Stopwatch sw = Stopwatch.StartNew();
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            var needsFlattening = ArchiveNeedsFlattening(archive);
            if (!archive.HasFiles() && !needsFlattening) return;
            
            archive.ExtractToDirectory(extractPath);
            _logger.LogDebug($"Extracted archive to {extractPath} in {sw.ElapsedMilliseconds} milliseconds.");

            if (needsFlattening)
            {
                sw = Stopwatch.StartNew();
                _logger.LogInformation("Extracted archive is nested in root folder, flattening...");
                new DirectoryInfo(extractPath).Flatten();
                _logger.LogInformation($"Flattened in {sw.ElapsedMilliseconds} milliseconds");
            }
        }
    }
}