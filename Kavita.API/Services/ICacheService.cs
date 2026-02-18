using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

public interface ICacheService
{
    /// <summary>
    /// Ensures the cache is created for the given chapter and if not, will create it. Should be called before any other
    /// cache operations (except cleanup).
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="extractPdfToImages">Extracts a PDF into images for a different reading experience</param>
    /// <param name="ct"></param>
    /// <returns>Chapter for the passed chapterId. Side-effect from ensuring cache.</returns>
    Task<Chapter?> Ensure(int chapterId, bool extractPdfToImages = false, CancellationToken ct = default);
    /// <summary>
    /// Clears cache directory of all volumes. This can be invoked from deleting a library or a series.
    /// </summary>
    /// <param name="chapterIds">Volumes that belong to that library. Assume the library might have been deleted before this invocation.</param>
    void CleanupChapters(IEnumerable<int> chapterIds);
    void CleanupBookmarks(IEnumerable<int> seriesIds);
    string GetCachedPagePath(int chapterId, int page);
    string GetCachePath(int chapterId);
    string GetBookmarkCachePath(int seriesId);
    IEnumerable<string> GetCachedPages(int chapterId);
    IEnumerable<FileDimensionDto> GetCachedFileDimensions(string cachePath);
    string GetCachedBookmarkPagePath(int seriesId, int page);
    string GetCachedFile(Chapter chapter);
    string GetCachedFile(int chapterId, string firstFilePath);
    public void ExtractChapterFiles(string extractPath, IReadOnlyList<MangaFile> files, bool extractPdfImages = false);
    Task<int> CacheBookmarkForSeries(int userId, int seriesId, CancellationToken ct = default);
    void CleanupBookmarkCache(int seriesId);
}
