using System.Threading;
using System.Threading.Tasks;
using API.Middleware;
using Microsoft.Extensions.Caching.Hybrid;

namespace API.Services.Caching;

public interface IAuthKeyCacheInvalidator
{
    /// <summary>
    /// Invalidates the cached authentication data for a specific auth key.
    /// Call this when a key is rotated or deleted.
    /// </summary>
    /// <param name="keyValue">The actual key value (not the ID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateAsync(string keyValue, CancellationToken cancellationToken = default);
}

public class AuthKeyCacheInvalidator(HybridCache cache) : IAuthKeyCacheInvalidator
{
    public async Task InvalidateAsync(string keyValue, CancellationToken cancellationToken = default)
    {
        var cacheKey = AuthKeyAuthenticationHandler.CreateCacheKey(keyValue);
        await cache.RemoveAsync(cacheKey, cancellationToken);
    }
}
