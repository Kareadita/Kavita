using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class AuthKeyService(IDataContext context, ILogger<AuthKeyService> logger, HybridCache cache) : IAuthKeyService
{
    public async Task UpdateLastAccessedAsync(string authKey, CancellationToken ct = default)
    {
        logger.LogTrace("Updating last accessed Auth key:  {AuthKey}", authKey);
        await context.AppUserAuthKey
            .Where(k => k.Key == authKey)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(k => k.LastAccessedAtUtc, DateTime.UtcNow), cancellationToken: ct);
    }

    public async Task InvalidateAsync(string keyValue, CancellationToken cancellationToken = default)
    {
        var cacheKey = CreateCacheKey(keyValue);
        await cache.RemoveAsync(cacheKey, cancellationToken);
    }

    public string CreateCacheKey(string keyValue)
    {
        return $"authKey_{keyValue}";
    }
}
