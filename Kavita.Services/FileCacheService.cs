using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common.Extensions;
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
        var safeKey = SanitizeKey(key);
        var path = GetPath(safeKey, cacheBucket);
        var sem = _locks.GetOrAdd(safeKey, _ => new SemaphoreSlim(1, 1));

        await sem.WaitAsync(ct);
        try
        {
            if (File.Exists(path) && DateTime.UtcNow - new FileInfo(path).LastWriteTimeUtc <= ttl)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, ct);
                    var cached = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    _logger.LogTrace("[FileCache] Hit: {Key}", safeKey.Sanitize());

                    return cached;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FileCache] Failed to deserialize cache for {Key}, fetching fresh", safeKey.Sanitize());
                }
            }

            _logger.LogTrace("[FileCache] Miss: {Key}", safeKey.Sanitize());
            var result = await fetch(ct);
            if (shouldCache != null && !shouldCache(result)) return result;

            try
            {
                var dir = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);
                var tmp = Path.Combine(dir, $"{safeKey}-{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(result, JsonOptions), ct);
                File.Move(tmp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FileCache] Failed to write cache for {Key}", safeKey.Sanitize());
            }

            return result;
        }
        finally
        {
            sem.Release();
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
}
