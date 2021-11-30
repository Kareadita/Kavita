using System;
using System.IO;
using API.Entities;
using API.Services;

namespace API.Helpers;

public static class CacheHelper
{
    // TODO: This will be all the code to check if I files need updating or not
    /// <summary>
    /// Determines whether an entity should regenerate cover image.
    /// </summary>
    /// <remarks>If a cover image is locked but the underlying file has been deleted, this will allow regenerating. </remarks>
    /// <param name="coverImage"></param>
    /// <param name="firstFile"></param>
    /// <param name="forceUpdate"></param>
    /// <param name="isCoverLocked"></param>
    /// <param name="coverImageDirectory">Directory where cover images are. Defaults to <see cref="DirectoryService.CoverImageDirectory"/></param>
    /// <returns></returns>
    public static bool ShouldUpdateCoverImage(string coverImage, MangaFile firstFile, DateTime chapterCreated, bool forceUpdate = false,
        bool isCoverLocked = false, string coverImageDirectory = null)
    {
        if (string.IsNullOrEmpty(coverImageDirectory))
        {
            coverImageDirectory = DirectoryService.CoverImageDirectory;
        }

        var fileExists = File.Exists(Path.Join(coverImageDirectory, coverImage));
        if (isCoverLocked && fileExists) return false;
        if (forceUpdate) return true;
        return (firstFile != null && (firstFile.HasFileBeenModifiedSince(chapterCreated) || firstFile.HasFileBeenModified())) || !HasCoverImage(coverImage, fileExists);
    }

    private static bool HasCoverImage(string coverImage)
    {
        return HasCoverImage(coverImage, File.Exists(coverImage));
    }

    private static bool HasCoverImage(string coverImage, bool fileExists)
    {
        return !string.IsNullOrEmpty(coverImage) && fileExists;
    }

    /// <summary>
    /// Determines if a given coverImage path exists
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool CoverImageExists(string path)
    {
        return File.Exists(path);
        //return !string.IsNullOrEmpty(path) && File.Exists(path);
    }
}
