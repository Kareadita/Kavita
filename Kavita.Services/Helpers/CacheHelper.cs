using System;
using Kavita.API.Services;
using Kavita.API.Services.Helpers;
using Kavita.Models.Entities;

namespace Kavita.Services.Helpers;

public class CacheHelper : ICacheHelper
{
    private readonly IFileService _fileService;

    public CacheHelper(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Determines whether an entity should regenerate cover image.
    /// </summary>
    /// <remarks>If a cover image is locked but the underlying file has been deleted, this will allow regenerating. </remarks>
    /// <param name="coverPath">This should just be the filename, no path information</param>
    /// <param name="sourceChanged">The file the cover is made from changed since the cover was made. Always false for Volume and Series</param>
    /// <param name="forceUpdate">If the user has told us to force the refresh</param>
    /// <param name="isCoverLocked">If cover has been locked by user. This will force false</param>
    /// <returns></returns>
    public bool ShouldUpdateCoverImage(string coverPath, bool sourceChanged, bool forceUpdate = false, bool isCoverLocked = false)
    {
        var fileExists = !string.IsNullOrEmpty(coverPath) && _fileService.Exists(coverPath);
        if (isCoverLocked && fileExists) return false;
        if (forceUpdate) return true;

        return sourceChanged || !fileExists;
    }

    /// <summary>
    /// Has the file been modified since last scan or is user forcing an update
    /// </summary>
    /// <param name="lastScan">Last time the scan was performed on this file</param>
    /// <param name="forceUpdate">Should we ignore any logic and force this to return true</param>
    /// <param name="firstFile">The file in question</param>
    /// <returns></returns>
    public bool HasFileChangedSinceLastScan(DateTime lastScan, bool forceUpdate, MangaFile? firstFile)
    {
        if (firstFile == null) return false;
        if (forceUpdate) return true;
        return _fileService.HasFileBeenModifiedSince(firstFile.FilePath, lastScan)
               || _fileService.HasFileBeenModifiedSince(firstFile.FilePath, firstFile.LastModified);
    }

    /// <summary>
    /// Determines if a given coverImage path exists
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool CoverImageExists(string path)
    {
        return !string.IsNullOrEmpty(path) && _fileService.Exists(path);
    }
}
