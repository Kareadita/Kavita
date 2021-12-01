using System;
using System.IO;
using API.Entities;
using API.Entities.Interfaces;
using API.Services;

namespace API.Helpers;

public interface ICacheHelper
{
    bool ShouldUpdateCoverImage(string coverImage, MangaFile firstFile, DateTime chapterCreated,
        bool forceUpdate = false,
        bool isCoverLocked = false, string coverImageDirectory = null);

    bool CoverImageExists(string path);

    bool HasFileNotChangedSinceCreationOrLastScan(IEntityDate chapter, bool forceUpdate, MangaFile firstFile);

}

public class CacheHelper : ICacheHelper
{
    private readonly FileService _fileService;

    public CacheHelper(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Determines whether an entity should regenerate cover image.
    /// </summary>
    /// <remarks>If a cover image is locked but the underlying file has been deleted, this will allow regenerating. </remarks>
    /// <param name="coverImage">This should just be the filename, no path information</param>
    /// <param name="firstFile"></param>
    /// <param name="forceUpdate">If the user has told us to force the refresh</param>
    /// <param name="isCoverLocked">If cover has been locked by user. This will force false</param>
    /// <param name="coverImageDirectory">Directory where cover images are. Defaults to <see cref="DirectoryService.CoverImageDirectory"/>. Only time different is for unit tests</param>
    /// <returns></returns>
    public bool ShouldUpdateCoverImage(string coverImage, MangaFile firstFile, DateTime chapterCreated, bool forceUpdate = false,
        bool isCoverLocked = false, string coverImageDirectory = null)
    {
        if (firstFile == null) return true;
        if (string.IsNullOrEmpty(coverImageDirectory))
        {
            coverImageDirectory = DirectoryService.CoverImageDirectory;
        }


        var filePath = Path.Join(coverImageDirectory, coverImage); // TODO: See if we can refactor the coverImageDirectory out
        var fileExists = _fileService.Exists(filePath);
        if (isCoverLocked && fileExists) return false;
        if (forceUpdate) return true;
        return _fileService.HasFileBeenModifiedSince(filePath, chapterCreated) || !fileExists;
        //return (firstFile.HasFileBeenModifiedSince(chapterCreated) || firstFile.HasFileBeenModified()) || !HasCoverImage(coverImage, fileExists);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="forceUpdate"></param>
    /// <param name="firstFile"></param>
    /// <returns></returns>
    public bool HasFileNotChangedSinceCreationOrLastScan(IEntityDate chapter, bool forceUpdate, MangaFile firstFile)
    {

        return firstFile == null || (!forceUpdate && !(!firstFile.HasFileBeenModifiedSince(chapter.Created) || firstFile.HasFileBeenModified()));
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
