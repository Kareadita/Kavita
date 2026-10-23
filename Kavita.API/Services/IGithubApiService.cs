using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Misc;

namespace Kavita.API.Services;

/// <summary>
/// Every GitHub call goes through here. Unauthenticated GitHub API calls share 60 per hour per IP, so responses are
/// cached and calls pause after a rate limit or failure.
/// </summary>
public interface IGithubApiService
{
    /// <summary>
    /// GET against api.github.com. Never throws on a GitHub failure: serves the cache at any age, or Unavailable.
    /// </summary>
    /// <param name="apiPath">Path after https://api.github.com/, also used as the cache key</param>
    /// <param name="ttl"></param>
    /// <param name="forceRefresh">Skip a cache within TTL. Still skips GitHub while calls are paused</param>
    /// <param name="ct"></param>
    Task<GithubResult<T>> GetAsync<T>(string apiPath, TimeSpan ttl, bool forceRefresh = false, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Every folder and file in the repo at gitRef, in one API call
    /// </summary>
    /// <param name="repo">Owner/Name</param>
    /// <param name="gitRef">Branch or commit sha</param>
    /// <param name="ttl"></param>
    /// <param name="ct"></param>
    Task<GithubResult<GithubTree>> GetTreeAsync(string repo, string gitRef, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Downloads a file from raw.githubusercontent.com, which does not count against the API rate limit.
    /// </summary>
    /// <param name="repo">Owner/Name</param>
    /// <param name="gitRef">Branch or commit sha</param>
    /// <param name="path">File path in the repo, unescaped</param>
    /// <param name="cacheTtl">Cache the file for this long, null to always download</param>
    /// <param name="ct"></param>
    Task<GithubResult<string>> DownloadRawAsync(string repo, string gitRef, string path, TimeSpan? cacheTtl = null,
        CancellationToken ct = default);

    /// <summary>
    /// The raw.githubusercontent.com url DownloadRawAsync fetches, for clients that download the file themselves
    /// </summary>
    string GetRawUrl(string repo, string gitRef, string path);

    void ClearCache();
}
