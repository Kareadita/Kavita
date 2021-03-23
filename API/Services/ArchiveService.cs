using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;
using API.Archive;
using API.Extensions;
using API.Interfaces.Services;
using API.Services.Tasks;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using Image = NetVips.Image;

namespace API.Services
{
    /// <summary>
    /// Responsible for manipulating Archive files. Used by <see cref="CacheService"/> and <see cref="ScannerService"/>
    /// </summary>
    public class ArchiveService : IArchiveService
    {
        private readonly ILogger<ArchiveService> _logger;
        private const int ThumbnailWidth = 320; // 153w x 230h TODO: Look into optimizing the images to be smaller

        public ArchiveService(ILogger<ArchiveService> logger)
        {
            _logger = logger;
        }
        
        public int GetNumberOfPagesFromArchive(string archivePath)
        {
            if (!IsValidArchive(archivePath))
            {
                _logger.LogError("Archive {ArchivePath} could not be found", archivePath);
                return 0;
            }
            var count = 0;

            var libraryHandler = Archive.Archive.CanOpen(archivePath);
            
            try
            {
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        _logger.LogDebug("Using default compression handling");
                        using ZipArchive archive = ZipFile.OpenRead(archivePath);
                        return archive.Entries.Count(e => Parser.Parser.IsImage(e.FullName));
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        return archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsImage(entry.Key)).Count();
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[GetNumberOfPagesFromArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return 0;
                    default:
                        _logger.LogError("[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return 0;
                }
                

                // using Stream stream = File.OpenRead(archivePath);
                // using (var reader = ReaderFactory.Open(stream))
                // {
                //     try
                //     {
                //         _logger.LogDebug("{ArchivePath}'s Type: {ArchiveType}", archivePath, reader.ArchiveType);
                //     }
                //     catch (InvalidOperationException ex)
                //     {
                //         _logger.LogError(ex, "Could not parse the archive. Please validate it is not corrupted, {ArchivePath}", archivePath);
                //         return 0;
                //     }
                //         
                //     while (reader.MoveToNextEntry())
                //     {
                //         if (!reader.Entry.IsDirectory && Parser.Parser.IsImage(reader.Entry.Key))
                //         {
                //             count++;
                //         }
                //     }
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                return 0;
            }
            
            
            
            
            try
            {
                using var archive = ArchiveFactory.Open(archivePath);
                return archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsImage(entry.Key)).Count();

                // using Stream stream = File.OpenRead(archivePath);
                // using (var reader = ReaderFactory.Open(stream))
                // {
                //     try
                //     {
                //         _logger.LogDebug("{ArchivePath}'s Type: {ArchiveType}", archivePath, reader.ArchiveType);
                //     }
                //     catch (InvalidOperationException ex)
                //     {
                //         _logger.LogError(ex, "Could not parse the archive. Please validate it is not corrupted, {ArchivePath}", archivePath);
                //         return 0;
                //     }
                //         
                //     while (reader.MoveToNextEntry())
                //     {
                //         if (!reader.Entry.IsDirectory && Parser.Parser.IsImage(reader.Entry.Key))
                //         {
                //             count++;
                //         }
                //     }
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                return 0;
            }

            return count;
        }

        public ArchiveMetadata GetArchiveData(string archivePath, bool createThumbnail)
        {
            return new ArchiveMetadata()
            {
                Pages = GetNumberOfPagesFromArchive(archivePath),
                //Summary = GetSummaryInfo(archivePath),
                CoverImage = GetCoverImage(archivePath, createThumbnail)
            };
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
                
                using var archive = ArchiveFactory.Open(filepath);
                return FindCoverImage(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsImage(entry.Key)), createThumbnail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetCoverImage] There was an exception when reading archive stream: {Filepath}. Defaulting to no cover image", filepath);
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
                    using var ms = new MemoryStream();
                    entry.WriteTo(ms);
                    ms.Position = 0;
                    return createThumbnail ? CreateThumbnail(ms.ToArray(), Path.GetExtension(entry.Key)) : ms.ToArray();
                }
            }

            if (images.Any())
            {
                var entry = images.OrderBy(e => e.Key).FirstOrDefault();
                if (entry == null) return Array.Empty<byte>();
                using var ms = new MemoryStream();
                entry.WriteTo(ms);
                ms.Position = 0;
                var data = ms.ToArray();
                return createThumbnail ? CreateThumbnail(data, Path.GetExtension(entry.Key)) : data;
            }
            
            return Array.Empty<byte>();
        }
        
        private byte[] CreateThumbnail(byte[] entry, string formatExtension = ".jpg")
        {
            if (!formatExtension.StartsWith("."))
            {
                formatExtension = "." + formatExtension;
            }
            // TODO: Validate if jpeg is same as jpg
            try
            {
                using var thumbnail = Image.ThumbnailBuffer(entry, ThumbnailWidth);
                return thumbnail.WriteToBuffer(formatExtension); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CreateThumbnail] There was a critical error and prevented thumbnail generation. Defaulting to no cover image");
            }

            return Array.Empty<byte>();
        }
        

        /// <summary>
        /// Test if the archive path exists and there are images inside it. This will log as an error. 
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
                if (Path.GetFileNameWithoutExtension(entry.Key).ToLower().EndsWith("comicinfo") && Parser.Parser.IsXml(entry.Key))
                {
                    using var ms = new MemoryStream();
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

                if (SharpCompress.Archives.Zip.ZipArchive.IsZipFile(archivePath))
                {
                    using var archive = SharpCompress.Archives.Zip.ZipArchive.Open(archivePath);
                    info = FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsXml(entry.Key)));
                }
                else if (GZipArchive.IsGZipFile(archivePath))
                {
                    using var archive = GZipArchive.Open(archivePath);
                    info = FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsXml(entry.Key)));
                }
                else if (RarArchive.IsRarFile(archivePath))
                {
                    using var archive = RarArchive.Open(archivePath);
                    info = FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsXml(entry.Key)));
                }
                else if (SevenZipArchive.IsSevenZipFile(archivePath))
                {
                    using var archive = SevenZipArchive.Open(archivePath);
                    info = FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsXml(entry.Key)));
                }
                else if (TarArchive.IsTarFile(archivePath))
                {
                    using var archive = TarArchive.Open(archivePath);
                    info = FindComicInfoXml(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsXml(entry.Key)));
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

        private void ExtractArchiveEntities(IEnumerable<IArchiveEntry> entries, string extractPath)
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
            
            // using ZipArchive archive = ZipFile.OpenRead(archivePath);
            // archive.ExtractToDirectory(extractPath, true);
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
            if (!File.Exists(archivePath)) return;

            if (new DirectoryInfo(extractPath).Exists) return;
            
            var sw = Stopwatch.StartNew();
            using var archive = ArchiveFactory.Open(archivePath);
            try
            {
                ExtractArchiveEntities(archive.Entries.Where(entry => !entry.IsDirectory && Parser.Parser.IsImage(entry.Key)), extractPath);
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