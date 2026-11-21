using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using Kavita.Common;
using Kavita.Models.DTOs.Archive;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Models.Metadata;

namespace Kavita.API.Services;

public interface IArchiveService
{
    void ExtractArchive(string archivePath, string extractPath);
    int GetNumberOfPagesFromArchive(string archivePath);
    /// <summary>
    /// Opens the archive's cover image: a folder or cover entry, else the first image. Null when the archive cannot be
    /// read or has no image, after filing a media issue
    /// </summary>
    OpenedCover? OpenCover(string archivePath);
    bool IsValidArchive(string archivePath);
    ComicInfo? GetComicInfo(string archivePath);
    ArchiveLibrary CanOpen(string archivePath);
    bool ArchiveNeedsFlattening(ZipArchive archive);
    /// <summary>
    /// Creates a zip file form the listed files and outputs to the temp folder. This will combine into one zip of multiple zips.
    /// </summary>
    /// <param name="files">List of files to be zipped up. Should be full file paths.</param>
    /// <param name="tempFolder">Temp folder name to use for preparing the files. Will be created and deleted</param>
    /// <returns>Path to the temp zip</returns>
    /// <exception cref="KavitaException"></exception>
    string CreateZipForDownload(IEnumerable<string> files, string tempFolder);

    /// <summary>
    /// Creates a zip file form the listed files and outputs to the temp folder. This will extract each archive and combine them into one zip.
    /// </summary>
    /// <param name="files">List of files to be zipped up. Should be full file paths.</param>
    /// <param name="tempFolder">Temp folder name to use for preparing the files. Will be created and deleted</param>
    /// <param name="progressCallback"></param>
    /// <returns>Path to the temp zip</returns>
    /// <exception cref="KavitaException"></exception>
    string CreateZipFromFoldersForDownload(IList<string> files, string tempFolder, Func<Tuple<string, float>, Task> progressCallback);
}
