using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Misc;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public partial class FileCacheService : IFileCacheService
{
    private readonly IDirectoryService _directoryService;
    private readonly ILogger<FileCacheService> _logger;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    [GeneratedRegex(@"[^a-z0-9_\-]")]
    private static partial Regex UnsafeKeyChars();

    public const string KavitaPlusCacheDirectory = "kplus";
    public const string VersionDirectory = "version";

    public FileCacheService(IDirectoryService directoryService, ILogger<FileCacheService> logger)
    {
        _directoryService = directoryService;
        _logger = logger;
    }

    public async Task<T?> GetOrFetchAsync<T>(string key, string cacheBucket, TimeSpan ttl,
        Func<CancellationToken, Task<T?>> fetch,
        Func<T?, bool>? shouldCache = null,
        CancellationToken ct = default)
    {
        var result = await GetOrFetchResultAsync(key, cacheBucket, ttl, fetch, shouldCache, ct: ct);
        return result.Value;
    }

    public async Task<FileCacheResult<T>> GetOrFetchResultAsync<T>(string key, string cacheBucket, TimeSpan ttl,
        Func<CancellationToken, Task<T?>> fetch,
        Func<T?, bool>? shouldCache = null,
        bool serveStaleOnFailure = false,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var safeKey = SanitizeKey(key);
        var path = GetPath(safeKey, cacheBucket);
        var sem = _locks.GetOrAdd(safeKey, _ => new SemaphoreSlim(1, 1));

        await sem.WaitAsync(ct);
        try
        {
            DateTime? cachedAtUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;

            if (!forceRefresh && DateTime.UtcNow - cachedAtUtc <= ttl)
            {
                var cached = await TryReadAsync<T>(path, safeKey, ct);
                if (cached.Found)
                {
                    _logger.LogTrace("[FileCache] Hit: {Key}", safeKey.Sanitize());
                    return new FileCacheResult<T> { Value = cached.Value, Source = CacheSource.Cache, CachedAtUtc = cachedAtUtc };
                }
            }

            _logger.LogTrace("[FileCache] Miss: {Key}", safeKey.Sanitize());

            T? result;
            try
            {
                result = await fetch(ct);
            }
            catch (Exception ex) when (serveStaleOnFailure && ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[FileCache] Fetch failed for {Key}", safeKey.Sanitize());
                return await StaleOrUnavailable<T>(path, safeKey, cachedAtUtc, default, ct);
            }

            if (shouldCache != null && !shouldCache(result))
            {
                if (serveStaleOnFailure)
                {
                    return await StaleOrUnavailable(path, safeKey, cachedAtUtc, result, ct);
                }

                return new FileCacheResult<T> { Value = result, Source = CacheSource.Unavailable };
            }

            var written = await TryWriteAsync(path, safeKey, result, ct);
            return new FileCacheResult<T> { Value = result, Source = CacheSource.Live, CachedAtUtc = written ? DateTime.UtcNow : null };
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<FileCacheResult<T>> StaleOrUnavailable<T>(string path, string safeKey, DateTime? cachedAtUtc,
        T? fetchResult, CancellationToken ct)
    {
        if (cachedAtUtc != null)
        {
            var stale = await TryReadAsync<T>(path, safeKey, ct);
            if (stale.Found)
            {
                _logger.LogDebug("[FileCache] Serving stale cache for {Key} from {CachedAtUtc}", safeKey.Sanitize(), cachedAtUtc);
                return new FileCacheResult<T> { Value = stale.Value, Source = CacheSource.StaleCache, CachedAtUtc = cachedAtUtc };
            }
        }

        return new FileCacheResult<T> { Value = fetchResult, Source = CacheSource.Unavailable };
    }

    private async Task<CacheRead<T>> TryReadAsync<T>(string path, string safeKey, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return new CacheRead<T>(true, JsonSerializer.Deserialize<T>(json, JsonOptions));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[FileCache] Failed to deserialize cache for {Key}", safeKey.Sanitize());
            return new CacheRead<T>(false, default);
        }
    }

    private async Task<bool> TryWriteAsync<T>(string path, string safeKey, T? value, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, $"{safeKey}-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, JsonOptions), ct);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileCache] Failed to write cache for {Key}", safeKey.Sanitize());
            try
            {
                File.Delete(tmp);
            }
            catch (Exception)
            {
                // Best effort, a temp file that is still locked is left behind
            }

            return false;
        }
    }

    public void Invalidate(string key, string cacheBucket)
    {
        var path = GetPath(SanitizeKey(key), cacheBucket);
        if (!File.Exists(path)) return;
        try
        {
            File.Delete(path);
            _logger.LogTrace("[FileCache] Invalidated: {Key}", SanitizeKey(key).Sanitize());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileCache] Failed to invalidate cache for {Key}", SanitizeKey(key).Sanitize());
        }
    }

    public void InvalidatePrefix(string prefix, string cacheBucket)
    {
        var safePrefix = SanitizeKey(prefix);
        var dir = Path.Combine(_directoryService.LongTermCacheDirectory, cacheBucket);
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.EnumerateFiles(dir, $"{safePrefix}*.json"))
        {
            try
            {
                File.Delete(file);
                _logger.LogTrace("[FileCache] Invalidated (prefix): {File}", Path.GetFileNameWithoutExtension(file).Sanitize());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FileCache] Failed to invalidate cache file {File}", file.Sanitize());
            }
        }
    }

    private string GetPath(string safeKey, string cacheDir) =>
        Path.Combine(_directoryService.LongTermCacheDirectory, cacheDir, $"{safeKey}.json");

    private static string SanitizeKey(string key) =>
        UnsafeKeyChars().Replace(key.ToLowerInvariant(), "-");

    private readonly record struct CacheRead<T>(bool Found, T? Value);
}
