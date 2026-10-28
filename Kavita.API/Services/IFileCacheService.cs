using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Misc;

namespace Kavita.API.Services;

public interface IFileCacheService
{
    /// <summary>
    /// Returns a cached value for the given key if it exists and is within TTL, otherwise calls fetch,
    /// caches the result, and returns it.
    /// </summary>
    /// <remarks>
    /// The key MUST NOT contain sensitive data (API keys, tokens, emails, license fragments, etc.). Keys are
    /// written to disk as filenames and emitted to logs in cleartext. Sanitization only ensures filesystem-safe
    /// characters and prevents log injection; it does NOT redact secrets. Key on stable, non-sensitive
    /// identifiers (e.g. entity ids) instead.
    /// </remarks>
    Task<T?> GetOrFetchAsync<T>(string key, string cacheBucket, TimeSpan ttl,
        Func<CancellationToken, Task<T?>> fetch,
        Func<T?, bool>? shouldCache = null,
        CancellationToken ct = default);

    /// <summary>
    /// <see cref="GetOrFetchAsync{T}"/>, reporting where the value came from and when it was cached.
    /// </summary>
    /// <param name="serveStaleOnFailure">When the fetch throws or shouldCache rejects its result, return the cached
    /// value</param>
    /// <param name="forceRefresh">Fetch even when the cache is within TTL</param>
    /// <remarks>Same key rules as <see cref="GetOrFetchAsync{T}"/>.</remarks>
    Task<FileCacheResult<T>> GetOrFetchResultAsync<T>(string key, string cacheBucket, TimeSpan ttl,
        Func<CancellationToken, Task<T?>> fetch,
        Func<T?, bool>? shouldCache = null,
        bool serveStaleOnFailure = false,
        bool forceRefresh = false,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the cache entry for the given key.
    /// </summary>
    /// <remarks>
    /// The key MUST NOT contain sensitive data. Keys are written to disk as filenames and emitted to logs in
    /// cleartext; sanitization does not redact secrets. See <see cref="GetOrFetchAsync{T}"/>.
    /// </remarks>
    void Invalidate(string key, string cacheBucket);

    /// <summary>
    /// Deletes all cache entries that start with a given key.
    /// </summary>
    /// <remarks>
    /// The prefix MUST NOT contain sensitive data. Keys are written to disk as filenames and emitted to logs in
    /// cleartext; sanitization does not redact secrets. See <see cref="GetOrFetchAsync{T}"/>.
    /// </remarks>
    /// <param name="prefix"></param>
    /// <param name="cacheBucket"></param>
    void InvalidatePrefix(string prefix, string cacheBucket);
}
