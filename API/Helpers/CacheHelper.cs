using System;
using API.Entities;
using API.Entities.Interfaces;
using API.Services;

namespace API.Helpers;
#nullable enable

public interface ICacheHelper
{
    bool ShouldUpdateCoverImage(string coverPath, MangaFile? firstFile, DateTime chapterCreated,
        bool forceUpdate = false,
        bool isCoverLocked = false);

    bool CoverImageExists(string path);

    bool IsFileUnmodifiedSinceCreationOrLastScan(IEntityDate chapter, bool forceUpdate, MangaFile? firstFile);
    bool HasFileChangedSinceLastScan(DateTime lastScan, bool forceUpdate, MangaFile? firstFile);

}

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
    /// <param name="firstFile"></param>
    /// <param name="chapterCreated">When the chapter was created (Not Used)</param>
    /// <param name="forceUpdate">If the user has told us to force the refresh</param>
    /// <param name="isCoverLocked">If cover has been locked by user. This will force false</param>
    /// <returns></returns>
    public bool ShouldUpdateCoverImage(string coverPath, MangaFile? firstFile, DateTime chapterCreated, bool forceUpdate = false,
        bool isCoverLocked = false)
    {

        var fileExists = !string.IsNullOrEmpty(coverPath) && _fileService.Exists(coverPath);
        if (isCoverLocked && fileExists) return false;
        if (forceUpdate) return true;
        if (firstFile == null) return true;
        return (_fileService.HasFileBeenModifiedSince(firstFile.FilePath, firstFile.LastModified)) || !fileExists;
    }

    /// <summary>
    /// Has the file been not been modified since last scan or is user forcing an update
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="forceUpdate"></param>
    /// <param name="firstFile"></param>
    /// <returns></returns>
    public bool IsFileUnmodifiedSinceCreationOrLastScan(IEntityDate chapter, bool forceUpdate, MangaFile? firstFile)
    {
        return firstFile != null &&
               (!forceUpdate &&
                !(_fileService.HasFileBeenModifiedSince(firstFile.FilePath, chapter.Created)
                  || _fileService.HasFileBeenModifiedSince(firstFile.FilePath, firstFile.LastModified)));
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
