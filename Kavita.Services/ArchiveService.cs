using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Archive;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Kavita.Services;

/// <summary>
/// Responsible for manipulating Archive files. Used by <see cref="CacheService"/> and <see cref="ScannerService"/>
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ArchiveService(
    ILogger<ArchiveService> logger,
    IDirectoryService directoryService,
    IImageService imageService,
    IMediaErrorService mediaErrorService)
    : IArchiveService
{
    private const string ComicInfoFilename = "ComicInfo.xml";

    /// <summary>
    /// Checks if a File can be opened. Requires up to 2 opens of the filestream.
    /// </summary>
    /// <param name="archivePath"></param>
    /// <returns></returns>
    public virtual ArchiveLibrary CanOpen(string archivePath)
    {
        if (string.IsNullOrEmpty(archivePath) || !(File.Exists(archivePath) && Parser.IsArchive(archivePath) || Parser.IsEpub(archivePath))) return ArchiveLibrary.NotSupported;

        var ext = directoryService.FileSystem.Path.GetExtension(archivePath).ToUpper();
        if (ext.Equals(".CBR") || ext.Equals(".RAR")) return ArchiveLibrary.SharpCompress;

        try
        {
            using var a2 = ZipFile.OpenRead(archivePath);
            return ArchiveLibrary.Default;
        }
        catch (Exception)
        {
            try
            {
                using var a1 = ArchiveFactory.OpenArchive(archivePath);
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
            logger.LogError("Archive {ArchivePath} could not be found", archivePath);
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
                    return archive.Entries.Count(e => !Parser.HasBlacklistedFolderInPath(e.FullName) && Parser.IsImage(e.FullName));
                }
                case ArchiveLibrary.SharpCompress:
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath);
                    return archive.Entries.Count(entry => !entry.IsDirectory &&
                                                          !Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                          && Parser.IsImage(entry.Key));
                }
                case ArchiveLibrary.NotSupported:
                    logger.LogWarning("[GetNumberOfPagesFromArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                    mediaErrorService.ReportMediaIssue(archivePath, MediaErrorProducer.ArchiveService, "File format not supported", string.Empty);
                    return 0;
                default:
                    logger.LogWarning("[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                    mediaErrorService.ReportMediaIssue(archivePath, MediaErrorProducer.ArchiveService, "File format not supported", string.Empty);
                    return 0;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[GetNumberOfPagesFromArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
            mediaErrorService.ReportMediaIssue(archivePath, MediaErrorProducer.ArchiveService,
                "This archive cannot be read or not supported", ex);
            return 0;
        }
    }

    /// <summary>
    /// Finds the first instance of a folder entry and returns it
    /// </summary>
    /// <param name="entryFullNames"></param>
    /// <returns>Entry name of match, null if no match</returns>
    public static string? FindFolderEntry(IEnumerable<string> entryFullNames)
    {
        var result = entryFullNames
            .Where(path => !(Path.EndsInDirectorySeparator(path) || Parser.HasBlacklistedFolderInPath(path) || path.StartsWith(Parser.MacOsMetadataFileStartsWith)))
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .FirstOrDefault(Parser.IsCoverImage);

        return string.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// Returns first entry that is an image and is not in a blacklisted folder path. Uses <see cref="EnumerableExtensions.OrderByNatural"/> for ordering files
    /// </summary>
    /// <param name="entryFullNames"></param>
    /// <param name="archiveName"></param>
    /// <returns>Entry name of match, null if no match</returns>
    public static string? FirstFileEntry(IEnumerable<string> entryFullNames, string archiveName)
    {
        // First check if there are any files that are not in a nested folder before just comparing by filename. This is needed
        // because NaturalSortComparer does not work with paths and doesn't seem 001.jpg as before chapter 1/001.jpg.
        var fullNames = entryFullNames
            .Where(path => !(Path.EndsInDirectorySeparator(path) || Parser.HasBlacklistedFolderInPath(path) || path.StartsWith(Parser.MacOsMetadataFileStartsWith)) && Parser.IsImage(path))
            .OrderByNatural(c => c.GetFullPathWithoutExtension())
            .ToList();
        if (fullNames.Count == 0) return null;

        var nonNestedFile = fullNames.Where(entry => (Path.GetDirectoryName(entry) ?? string.Empty).Equals(archiveName))
            .OrderByNatural(c => c.GetFullPathWithoutExtension())
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(nonNestedFile)) return nonNestedFile;

        // Check the first folder and sort within that to see if we can find a file, else fallback to first file with basic sort.
        // Get first folder, then sort within that
        var firstDirectoryFile = fullNames.OrderByNatural(Path.GetDirectoryName!).FirstOrDefault();
        if (!string.IsNullOrEmpty(firstDirectoryFile))
        {
            var firstDirectory = Path.GetDirectoryName(firstDirectoryFile);
            if (!string.IsNullOrEmpty(firstDirectory))
            {
                var firstDirectoryResult = fullNames.Where(f => firstDirectory.Equals(Path.GetDirectoryName(f)))
                    .OrderByNatural(Path.GetFileNameWithoutExtension)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(firstDirectoryResult)) return firstDirectoryResult;
            }
        }

        var result = fullNames
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .FirstOrDefault();

        return string.IsNullOrEmpty(result) ? null : result;
    }


    /// <summary>
    /// Generates byte array of cover image.
    /// Given a path to a compressed file <see cref="Scanner.Parser.Parser.ArchiveFileExtensions"/>, will ensure the first image (respects directory structure) is returned unless
    /// a folder/cover.(image extension) exists in the the compressed file (if duplicate, the first is chosen)
    ///
    /// This skips over any __MACOSX folder/file iteration.
    /// </summary>
    /// <remarks>This always creates a thumbnail</remarks>
    /// <param name="archivePath"></param>
    /// <param name="fileName">File name to use based on context of entity.</param>
    /// <param name="outputDirectory">Where to output the file, defaults to covers directory</param>
    /// <param name="format">When saving the file, use encoding</param>
    /// <returns></returns>
    public string GetCoverImage(string archivePath, string fileName, string outputDirectory, EncodeFormat format, CoverImageSize size = CoverImageSize.Default)
    {
        if (string.IsNullOrEmpty(archivePath) || !IsValidArchive(archivePath)) return string.Empty;
        try
        {
            var libraryHandler = CanOpen(archivePath);
            switch (libraryHandler)
            {
                case ArchiveLibrary.Default:
                {
                    using var archive = ZipFile.OpenRead(archivePath);

                    var entryName = FindCoverImageFilename(archivePath, archive.Entries.Select(e => e.FullName));
                    var entry = archive.Entries.Single(e => e.FullName == entryName);

                    using var stream = entry.Open();
                    return imageService.WriteCoverThumbnail(stream, fileName, outputDirectory, format, size);
                }
                case ArchiveLibrary.SharpCompress:
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath);
                    var entryNames = archive.Entries.Where(archiveEntry => !archiveEntry.IsDirectory).Select(e => e.Key).ToList();

                    var entryName = FindCoverImageFilename(archivePath, entryNames);
                    if (entryName == null) return string.Empty;
                    var entry = archive.Entries.Single(e => e.Key == entryName);

                    using var stream = entry.OpenEntryStream();
                    return imageService.WriteCoverThumbnail(stream, fileName, outputDirectory, format, size);
                }
                case ArchiveLibrary.NotSupported:
                    logger.LogWarning("[GetCoverImage] This archive cannot be read: {ArchivePath}. Defaulting to no cover image", archivePath);
                    return string.Empty;
                default:
                    logger.LogWarning("[GetCoverImage] There was an exception when reading archive stream: {ArchivePath}. Defaulting to no cover image", archivePath);
                    return string.Empty;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[GetCoverImage] There was an exception when reading archive stream: {ArchivePath}. Defaulting to no cover image", archivePath);
            mediaErrorService.ReportMediaIssue(archivePath, MediaErrorProducer.ArchiveService,
                "This archive cannot be read or not supported", ex); // TODO: Localize this. Which user?
        }

        return string.Empty;
    }

    /// <summary>
    /// Given a list of image paths (assume within an archive), find the filename that corresponds to the cover
    /// </summary>
    /// <param name="archivePath"></param>
    /// <param name="entryNames"></param>
    /// <returns></returns>
    public static string? FindCoverImageFilename(string archivePath, IEnumerable<string> entryNames)
    {
        var entryName = FindFolderEntry(entryNames) ?? FirstFileEntry(entryNames, Path.GetFileName(archivePath));
        return entryName;
    }

    /// <summary>
    /// Given an archive stream, will assess whether directory needs to be flattened so that the extracted archive files are directly
    /// under extract path and not nested in subfolders. See <see cref="DirectoryService"/> Flatten method.
    /// </summary>
    /// <param name="archive">An opened archive stream</param>
    /// <returns></returns>
    public bool ArchiveNeedsFlattening(ZipArchive archive)
    {
        // Sometimes ZipArchive will list the directory and others it will just keep it in the FullName
        return archive.Entries.Count > 0 &&
               !Path.HasExtension(archive.Entries[0].FullName) ||
               archive.Entries.Any(e => e.FullName.Contains(Path.AltDirectorySeparatorChar) && !Parser.HasBlacklistedFolderInPath(e.FullName));
    }

    /// <summary>
    /// Creates a zip file form the listed files and outputs to the temp folder.
    /// </summary>
    /// <param name="files">List of files to be zipped up. Should be full file paths.</param>
    /// <param name="tempFolder">Temp folder name to use for preparing the files. Will be created and deleted</param>
    /// <returns>Path to the temp zip</returns>
    /// <exception cref="KavitaException"></exception>
    public string CreateZipForDownload(IEnumerable<string> files, string tempFolder)
    {
        var dateString = DateTime.UtcNow.ToShortDateString().Replace("/", "_");

        var tempLocation = Path.Join(directoryService.TempDirectory, $"{tempFolder}_{dateString}");
        var potentialExistingFile = directoryService.FileSystem.FileInfo.New(Path.Join(directoryService.TempDirectory, $"kavita_{tempFolder}_{dateString}.zip"));
        if (potentialExistingFile.Exists)
        {
            // A previous download exists, just return it immediately
            return potentialExistingFile.FullName;
        }

        directoryService.ExistOrCreate(tempLocation);

        if (!directoryService.CopyFilesToDirectory(files, tempLocation))
        {
            throw new KavitaException("bad-copy-files-for-download");
        }

        var zipPath = Path.Join(directoryService.TempDirectory, $"kavita_{tempFolder}_{dateString}.zip");
        try
        {
            ZipFile.CreateFromDirectory(tempLocation, zipPath);
            // Remove the folder as we have the zip
            directoryService.ClearAndDeleteDirectory(tempLocation);
        }
        catch (AggregateException ex)
        {
            logger.LogError(ex, "There was an issue creating temp archive");
            throw new KavitaException("generic-create-temp-archive");
        }

        return zipPath;
    }

    public string CreateZipFromFoldersForDownload(IList<string> files, string tempFolder, Func<Tuple<string, float>, Task> progressCallback)
    {
        var dateString = DateTime.UtcNow.ToShortDateString().Replace("/", "_");

        var potentialExistingFile = directoryService.FileSystem.FileInfo.New(Path.Join(directoryService.TempDirectory, $"kavita_{tempFolder}_{dateString}.cbz"));
        if (potentialExistingFile.Exists)
        {
            // A previous download exists, just return it immediately
            return potentialExistingFile.FullName;
        }

        // Extract all the files to a temp directory and create zip on that
        var tempLocation = Path.Join(directoryService.TempDirectory, $"{tempFolder}_{dateString}");
        var totalFiles = files.Count + 1;
        var count = 1f;
        try
        {
            directoryService.ExistOrCreate(tempLocation);
            foreach (var path in files)
            {
                var tempPath = Path.Join(tempLocation, directoryService.FileSystem.Path.GetFileNameWithoutExtension(directoryService.FileSystem.FileInfo.New(path).Name));

                // Image series need different handling
                if (Parser.IsImage(path))
                {
                    var parentDirectory = directoryService.FileSystem.DirectoryInfo.New(path).Parent?.Name;
                    tempPath = Path.Join(tempLocation, parentDirectory ?? directoryService.FileSystem.FileInfo.New(path).Name);
                }

                if (Parser.IsArchive(path))
                {
                    // Archives don't need to be put into a subdirectory of the same name
                    tempPath = directoryService.GetParentDirectoryName(tempPath);
                }

                progressCallback(Tuple.Create(directoryService.FileSystem.FileInfo.New(path).Name, (1.0f * totalFiles) / count));

                directoryService.CopyFileToDirectory(path, tempPath);
                count++;
            }
        }
        catch
        {
            throw new KavitaException("bad-copy-files-for-download");
        }

        var zipPath = Path.Join(directoryService.TempDirectory, $"kavita_{tempFolder}_{dateString}.cbz");
        try
        {
            ZipFile.CreateFromDirectory(tempLocation, zipPath);
            // Remove the folder as we have the zip
            directoryService.ClearAndDeleteDirectory(tempLocation);
        }
        catch (AggregateException ex)
        {
            logger.LogError(ex, "There was an issue creating temp archive");
            throw new KavitaException("generic-create-temp-archive");
        }

        return zipPath;
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
            logger.LogWarning("Archive {ArchivePath} could not be found", archivePath);
            return false;
        }

        if (Parser.IsArchive(archivePath)) return true;

        logger.LogWarning("Archive {ArchivePath} is not a valid archive", archivePath);
        return false;
    }

    private static bool IsComicInfoArchiveEntry(string? fullName, string name)
    {
        if (fullName == null) return false;
        return !Parser.HasBlacklistedFolderInPath(fullName)
               && name.EndsWith(ComicInfoFilename, StringComparison.OrdinalIgnoreCase)
               && !name.StartsWith(Parser.MacOsMetadataFileStartsWith);
    }

    /// <summary>
    /// This can be null if nothing is found or any errors occur during access
    /// </summary>
    /// <param name="archivePath"></param>
    /// <returns></returns>
    public ComicInfo? GetComicInfo(string archivePath)
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

                    var entry = archive.Entries.FirstOrDefault(x => (x.FullName ?? x.Name) == ComicInfoFilename) ??
                        archive.Entries.FirstOrDefault(x => IsComicInfoArchiveEntry(x.FullName, x.Name));
                    if (entry != null)
                    {
                        using var stream = entry.Open();
                        return Deserialize(stream);
                    }

                    break;
                }
                case ArchiveLibrary.SharpCompress:
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath);
                    var entry = archive.Entries.FirstOrDefault(entry => entry.Key == ComicInfoFilename) ??
                        archive.Entries.FirstOrDefault(entry =>
                        IsComicInfoArchiveEntry(Path.GetDirectoryName(entry.Key), entry.Key));

                    if (entry != null)
                    {
                        using var stream = entry.OpenEntryStream();
                        var info = Deserialize(stream);
                        return info;
                    }

                    break;
                }
                case ArchiveLibrary.NotSupported:
                    logger.LogWarning("[GetComicInfo] This archive cannot be read: {ArchivePath}", archivePath);
                    return null;
                default:
                    logger.LogWarning(
                        "[GetComicInfo] There was an exception when reading archive stream: {ArchivePath}",
                        archivePath);
                    return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[GetComicInfo] There was an exception when reading archive stream: {Filepath}", archivePath);
            mediaErrorService.ReportMediaIssue(archivePath, MediaErrorProducer.ArchiveService,
                "This archive cannot be read or not supported", ex);
        }

        return null;
    }

    /// <summary>
    /// Strips out empty tags before deserializing
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private static ComicInfo? Deserialize(Stream stream)
    {
        var comicInfoXml = XDocument.Load(stream);
        comicInfoXml.Descendants()
            .Where(e => e.IsEmpty || string.IsNullOrWhiteSpace(e.Value))
            .Remove();

        var serializer = new XmlSerializer(typeof(ComicInfo));
        using var reader = comicInfoXml.Root?.CreateReader();
        if (reader == null) return null;

        var info  = (ComicInfo?) serializer.Deserialize(reader);

        info.CleanComicInfo();

        return info;

    }


    private void ExtractArchiveEntities(IEnumerable<IArchiveEntry> entries, string extractPath)
    {
        directoryService.ExistOrCreate(extractPath);
        // TODO: Look into a Parallel.ForEach
        foreach (var entry in entries)
        {
            entry.WriteToDirectory(extractPath, new ExtractionOptions()
            {
                ExtractFullPath = true, // Don't flatten, let the flattener ensure correct order of nested folders
                Overwrite = false
            });
        }
    }

    private void ExtractArchiveEntries(ZipArchive archive, string extractPath)
    {
        var needsFlattening = ArchiveNeedsFlattening(archive);
        if (!archive.HasFiles() && !needsFlattening) return;

        archive.ExtractToDirectory(extractPath, true);
        if (!needsFlattening) return;

        logger.LogDebug("Extracted archive is nested in root folder, flattening...");
        directoryService.Flatten(extractPath);
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

        if (directoryService.FileSystem.Directory.Exists(extractPath)) return;

        if (!directoryService.FileSystem.File.Exists(archivePath))
        {
            logger.LogError("{Archive} does not exist on disk", archivePath);
            throw new KavitaException($"{archivePath} does not exist on disk");
        }

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
                    using var archive = ArchiveFactory.OpenArchive(archivePath);
                    ExtractArchiveEntities(archive.Entries.Where(entry => !entry.IsDirectory
                                                                          && !Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(entry.Key) ?? string.Empty)
                                                                          && Parser.IsImage(entry.Key)), extractPath);
                    break;
                }
                case ArchiveLibrary.NotSupported:
                    logger.LogWarning("[ExtractArchive] This archive cannot be read: {ArchivePath}", archivePath);
                    return;
                default:
                    logger.LogWarning("[ExtractArchive] There was an exception when reading archive stream: {ArchivePath}", archivePath);
                    return;
            }

        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ExtractArchive] There was a problem extracting {ArchivePath} to {ExtractPath}",archivePath, extractPath);
            mediaErrorService.ReportMediaIssue(archivePath, MediaErrorProducer.ArchiveService,
                "This archive cannot be read or not supported", ex);
            throw new KavitaException(
                $"There was an error when extracting {archivePath}. Check the file exists, has read permissions or the server OS can support all path characters.");
        }
        logger.LogDebug("Extracted archive to {ExtractPath} in {ElapsedMilliseconds} milliseconds", extractPath, sw.ElapsedMilliseconds);
    }
}
