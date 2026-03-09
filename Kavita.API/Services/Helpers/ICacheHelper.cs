using System;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.API.Services.Helpers;

public interface ICacheHelper
{
    bool ShouldUpdateCoverImage(string coverPath, MangaFile? firstFile, DateTime chapterCreated,
        bool forceUpdate = false,
        bool isCoverLocked = false);

    bool CoverImageExists(string path);

    bool IsFileUnmodifiedSinceCreationOrLastScan(IEntityDate chapter, bool forceUpdate, MangaFile? firstFile);
    bool HasFileChangedSinceLastScan(DateTime lastScan, bool forceUpdate, MangaFile? firstFile);

}
