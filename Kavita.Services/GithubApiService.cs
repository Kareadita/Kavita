using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Services;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Misc;
using Kavita.Models.Misc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class GithubApiService : IGithubApiService
{
    public const string CacheBucket = "github";

    private const string ApiBase = "https://api.github.com/";
    private const string RawBase = "https://raw.githubusercontent.com/";
    private const string PausedUntilKey = "github-api-paused-until";
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(15);
    // GitHub's primary window is 1 hour, so a reset further out than that is a bad header
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly IFileCacheService _fileCacheService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GithubApiService> _logger;

    public GithubApiService(IFileCacheService fileCacheService, IMemoryCache memoryCache, ILogger<GithubApiService> logger)
    {
        _fileCacheService = fileCacheService;
        _memoryCache = memoryCache;
        _logger = logger;

        FlurlConfiguration.ConfigureClientForUrl(ApiBase);
        FlurlConfiguration.ConfigureClientForUrl(RawBase);
    }

    public async Task<GithubResult<T>> GetAsync<T>(string apiPath, TimeSpan ttl, bool forceRefresh = false, CancellationToken ct = default)
        where T : class
    {
        var rateLimit = new GithubRateLimitDto();

        var result = await _fileCacheService.GetOrFetchResultAsync<T>(apiPath, CacheBucket, ttl,
            async token =>
            {
                if (GetPausedUntil() != null) return null;

                try
                {
                    var response = await WithGithubHeaders(ApiBase + apiPath).GetAsync(cancellationToken: token);
                    rateLimit = GetRateLimit(response);
                    if (rateLimit is {IsLow: true, IsExhausted: false})
                    {
                        _logger.LogWarning("GitHub API rate limit is low: {Remaining}/{Limit}, resets at {ResetsAt}",
                            rateLimit.Remaining, rateLimit.Limit, rateLimit.ResetsAtUtc);
                    }

                    return await response.GetJsonAsync<T>();
                }
                catch (FlurlHttpException ex) when (IsGithubUnavailable(ex))
                {
                    Pause(ex);
                    return null;
                }
                catch (FlurlHttpException ex)
                {
                    _logger.LogWarning("GitHub returned HTTP {StatusCode} for {ApiPath}", ex.StatusCode, apiPath);
                    return null;
                }
            },
            shouldCache: value => value != null,
            serveStaleOnFailure: true,
            forceRefresh: forceRefresh,
            ct: ct);

        return new GithubResult<T>
        {
            Value = result.Value,
            Source = result.Source,
            FetchedAtUtc = result.Source == CacheSource.Live ? DateTime.UtcNow : result.CachedAtUtc,
            RetryAtUtc = GetPausedUntil(),
            RateLimit = rateLimit
        };
    }

    public Task<GithubResult<GithubTree>> GetTreeAsync(string repo, string gitRef, TimeSpan ttl, CancellationToken ct = default)
    {
        return GetAsync<GithubTree>($"repos/{repo}/git/trees/{gitRef}?recursive=1", ttl, ct: ct);
    }

    public async Task<GithubResult<string>> DownloadRawAsync(string repo, string gitRef, string path, TimeSpan? cacheTtl = null,
        CancellationToken ct = default)
    {
        var url = GetRawUrl(repo, gitRef, path);

        if (cacheTtl == null)
        {
            var content = await DownloadRaw(url, ct);
            return content == null
                ? new GithubResult<string> { Source = CacheSource.Unavailable }
                : new GithubResult<string> { Value = content, Source = CacheSource.Live, FetchedAtUtc = DateTime.UtcNow };
        }

        var result = await _fileCacheService.GetOrFetchResultAsync($"raw-{repo}-{gitRef}-{path}", CacheBucket, cacheTtl.Value,
            token => DownloadRaw(url, token),
            shouldCache: content => content != null,
            serveStaleOnFailure: true,
            ct: ct);

        return new GithubResult<string>
        {
            Value = result.Value,
            Source = result.Source,
            FetchedAtUtc = result.Source == CacheSource.Live ? DateTime.UtcNow : result.CachedAtUtc
        };
    }

    public string GetRawUrl(string repo, string gitRef, string path)
    {
        var escapedPath = string.Join('/', path.Trim('/').Split('/').Select(Uri.EscapeDataString));
        return $"{RawBase}{repo}/{gitRef}/{escapedPath}";
    }

    private async Task<string?> DownloadRaw(string url, CancellationToken ct)
    {
        try
        {
            return await WithGithubHeaders(url).GetStringAsync(cancellationToken: ct);
        }
        catch (FlurlHttpException ex)
        {
            _logger.LogWarning(ex, "Failed to download {Url} from GitHub: {Error}", url, ex.Message);
            return null;
        }
    }

    public void ClearCache()
    {
        _fileCacheService.InvalidatePrefix(string.Empty, CacheBucket);
    }

    private DateTime? GetPausedUntil()
    {
        return _memoryCache.TryGetValue(PausedUntilKey, out DateTime until) && until > DateTime.UtcNow ? until : null;
    }

    private static IFlurlRequest WithGithubHeaders(string url)
    {
        return url
            .WithHeader("User-Agent", "Kavita")
            .WithHeader("Accept", "application/vnd.github.v3+json");
    }

    private static GithubRateLimitDto GetRateLimit(IFlurlResponse response)
    {
        var resetEpoch = response.Headers.FirstOrDefault("X-RateLimit-Reset");

        return new GithubRateLimitDto
        {
            Remaining = int.TryParse(response.Headers.FirstOrDefault("X-RateLimit-Remaining"), out var remaining) ? remaining : null,
            Limit = int.TryParse(response.Headers.FirstOrDefault("X-RateLimit-Limit"), out var limit) ? limit : null,
            ResetsAtUtc = long.TryParse(resetEpoch, out var epoch) ? DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime : null
        };
    }

    private static bool IsGithubUnavailable(FlurlHttpException ex)
    {
        return ex.StatusCode is null or 403 or 429 or >= 500;
    }

    private void Pause(FlurlHttpException ex)
    {
        var now = DateTime.UtcNow;
        var until = now + FailureBackoff;
        var reason = ex.Message;

        if (ex is { StatusCode: 403 or 429, Call.Response: not null})
        {
            var rateLimit = GetRateLimit(ex.Call.Response);
            if (rateLimit.IsExhausted && rateLimit.ResetsAtUtc > now)
            {
                until = rateLimit.ResetsAtUtc.Value;
                reason = "rate limit exhausted";
            }
            else if (int.TryParse(ex.Call.Response.Headers.FirstOrDefault("Retry-After"), out var retryAfterSeconds))
            {
                // Secondary rate limits answer with Retry-After instead of X-RateLimit-Remaining: 0
                until = now.AddSeconds(retryAfterSeconds);
                reason = "secondary rate limit";
            }
            else
            {
                reason = $"HTTP {ex.StatusCode}";
            }
        }

        if (until > now + MaxBackoff)
        {
            until = now + MaxBackoff;
        }

        _memoryCache.Set(PausedUntilKey, until, new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(until));

        _logger.LogWarning("GitHub API unavailable ({Reason}), pausing GitHub calls until {RetryAtUtc} UTC", reason, until);
    }
}
