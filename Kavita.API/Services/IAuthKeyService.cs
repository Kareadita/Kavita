using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface IAuthKeyService
{
    Task UpdateLastAccessedAsync(string authKey, CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached authentication data for a specific auth key.
    /// Call this when a key is rotated or deleted.
    /// </summary>
    /// <param name="keyValue">The actual key value (not the ID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateAsync(string keyValue, CancellationToken cancellationToken = default);

    string CreateCacheKey(string keyValue);
}
