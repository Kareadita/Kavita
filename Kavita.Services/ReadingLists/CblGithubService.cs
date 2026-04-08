using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Misc;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Services.Extensions;
using Microsoft.Extensions.Logging;


namespace Kavita.Services.ReadingLists;

internal sealed record GithubContentItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// File-backed cache structure. Each directory path maps to its cached listing + fetch timestamp.
/// </summary>
public sealed record CblRepoCache
{
    public Dictionary<string, CachedDirectory> Directories { get; init; } = new();
}

public sealed record CachedDirectory
{
    public DateTime FetchedAtUtc { get; init; }
    public List<CblRepoItemDto> Items { get; init; } = [];
}


public class CblGithubService : ICblGithubService
{
    private const string RepoOwner = "DieselTech";
    private const string RepoName = "CBL-ReadingLists";
    private const string ApiBase = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents";
    private const string CblExtension = ".cbl";
    private const string Cblv2Extension = ".json";
    private const string CacheFileName = "cbl-repo-cache.json";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    private readonly IDirectoryService _directoryService;
    private readonly ILogger<CblGithubService> _logger;

    public CblGithubService(IDirectoryService directoryService, ILogger<CblGithubService> logger)
    {
        _directoryService = directoryService;
        _logger = logger;

        FlurlConfiguration.ConfigureClientForUrl(ApiBase);
    }

    public async Task<CblRepoBrowseResultDto> BrowseRepo(string path = "", bool forceRefresh = false)
    {
        var normalizedPath = NormalizePath(path);
        var cache = forceRefresh ? new CblRepoCache() : LoadCache();

        if (!forceRefresh && cache.Directories.TryGetValue(normalizedPath, out var cached))
        {
            if (DateTime.UtcNow - cached.FetchedAtUtc < CacheTtl)
            {
                _logger.LogDebug("Cache hit for CBL repo path: {Path}", normalizedPath.Sanitize());
                return new CblRepoBrowseResultDto
                {
                    Items = cached.Items,
                    FromCache = true
                };
            }

            _logger.LogDebug("Cache expired for CBL repo path: {Path}", normalizedPath.Sanitize());
        }

        var (items, rateLimit) = await FetchDirectoryFromGithub(normalizedPath);

        cache.Directories[normalizedPath] = new CachedDirectory
        {
            FetchedAtUtc = DateTime.UtcNow,
            Items = items
        };

        SaveCache(cache);

        return new CblRepoBrowseResultDto
        {
            Items = items,
            RateLimitDto = rateLimit,
            FromCache = false
        };
    }


    public void InvalidateCache()
    {
        var cachePath = GetCacheFilePath();
        if (_directoryService.FileSystem.File.Exists(cachePath))
        {
            _directoryService.FileSystem.File.Delete(cachePath);
        }

        _logger.LogInformation("CBL repo cache invalidated");
    }

    public async Task<string> GetFileSha(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);

        var item = await BuildApiUrl(normalizedPath)
            .WithGithubHeaders()
            .GetJsonAsync<GithubContentItem>();

        return item.Sha;
    }

    public async Task<string> GetFileContent(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);

        var item = await BuildApiUrl(normalizedPath)
            .WithGithubHeaders()
            .GetJsonAsync<GithubContentItem>();

        if (string.IsNullOrEmpty(item.DownloadUrl))
        {
            throw new KavitaException($"No download URL available for {filePath}");
        }

        return await DownloadByUrl(item.DownloadUrl);
    }

    /// <summary>
    /// Downloads the content of a file
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    private static async Task<string> DownloadByUrl(string url)
    {
        return await url
            .WithGithubHeaders()
            .GetStringAsync();
    }

    private async Task<(List<CblRepoItemDto> Items, GithubRateLimitDto RateLimit)> FetchDirectoryFromGithub(string path)
    {
        _logger.LogDebug("Fetching CBL repo directory from GitHub: {Path}", path.Sanitize());

        try
        {
            var response = await BuildApiUrl(path)
                .WithGithubHeaders()
                .GetAsync();

            var rateLimit = response.GetRateLimit();

            if (rateLimit is {IsLow: true, IsExhausted: false})
            {
                _logger.LogWarning(
                    "GitHub API rate limit is low: {Remaining}/{Limit}, resets at {ResetsAt}",
                    rateLimit.Remaining, rateLimit.Limit, rateLimit.ResetsAtUtc);
            }

            var items = await response.GetJsonAsync<List<GithubContentItem>>();

            var result = items
                .Where(i => !i.Name.StartsWith('.')) // Insure .github or other meta files/directories are excluded
                .Where(i => i.Type == "dir" || i.Name.EndsWith(CblExtension, StringComparison.OrdinalIgnoreCase) || i.Name.EndsWith(Cblv2Extension, StringComparison.OrdinalIgnoreCase))
                .Select(i => new CblRepoItemDto
                {
                    Name = i.Name,
                    Path = i.Path,
                    IsDirectory = i.Type == "dir",
                    Sha = i.Sha,
                    Size = i.Size,
                    DownloadUrl = i.DownloadUrl
                })
                .OrderBy(i => !i.IsDirectory)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (result, rateLimit);
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 403)
        {
            var rateLimit = ex.Call?.Response != null
                ? ex.Call.Response.GetRateLimit()
                : new GithubRateLimitDto();

            if (rateLimit.IsExhausted)
            {
                var resetsIn = rateLimit.ResetsAtUtc.HasValue
                    ? $" Resets at {rateLimit.ResetsAtUtc.Value:HH:mm} UTC."
                    : string.Empty;

                _logger.LogWarning("GitHub API rate limit exhausted.{ResetsIn}", resetsIn);
                throw new KavitaException(
                    $"GitHub API rate limit exhausted.{resetsIn} Cached data may still be available.");
            }

            _logger.LogWarning(ex, "GitHub API returned 403 for CBL repo");
            throw new KavitaException("GitHub API access denied. Please try again later.");
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404)
        {
            _logger.LogWarning("CBL repo path not found: {Path}", path.Sanitize());
            throw new KavitaException($"Path not found in CBL repository: {path}");
        }
    }

    private CblRepoCache LoadCache()
    {
        var cachePath = GetCacheFilePath();
        if (!File.Exists(cachePath)) return new CblRepoCache();

        try
        {
            var json = File.ReadAllText(cachePath);
            return JsonSerializer.Deserialize<CblRepoCache>(json) ?? new CblRepoCache();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read CBL repo cache, starting fresh");
            return new CblRepoCache();
        }
    }

    private void SaveCache(CblRepoCache cache)
    {
        try
        {
            var cachePath = GetCacheFilePath();
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(cachePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write CBL repo cache");
        }
    }

    private string GetCacheFilePath()
    {
        return Path.Combine(_directoryService.LongTermCacheDirectory, CacheFileName);
    }

    private static string BuildApiUrl(string path)
    {
        if (string.IsNullOrEmpty(path)) return ApiBase;

        var encodedSegments = path.Split('/')
            .Select(Uri.EscapeDataString);
        return $"{ApiBase}/{string.Join("/", encodedSegments)}";
    }

    private static string NormalizePath(string path)
    {
        return path.Trim('/').Trim();
    }
}
