using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.ReadingLists.CBL;

namespace Kavita.API.Services.ReadingLists;

public interface ICblGithubService
{
    /// <summary>
    /// Browse a directory in the CBL repo. Returns folders and .cbl files only.
    /// Results are cached per-directory with TTL. Pass forceRefresh to bypass cache.
    /// </summary>
    Task<CblRepoBrowseResultDto> BrowseRepo(string path = "", bool forceRefresh = false);
    /// <summary>
    /// Returns the Git blob SHA for a file without downloading its content.
    /// </summary>
    Task<string> GetFileSha(string filePath);
    /// <summary>
    /// Downloads the raw content of a .cbl file by its repo path.
    /// </summary>
    Task<string> GetFileContent(string filePath);
    /// <summary>
    /// Invalidates all cached directory listings, forcing fresh fetches on next browse.
    /// </summary>
    void InvalidateCache();
}
