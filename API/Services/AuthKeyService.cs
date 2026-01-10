using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IAuthKeyService
{
    Task UpdateLastAccessedAsync(string authKey);
}

public class AuthKeyService(DataContext context, ILogger<AuthKeyService> logger) : IAuthKeyService
{
    public async Task UpdateLastAccessedAsync(string authKey)
    {
        logger.LogTrace("Updating last accessed Auth key:  {AuthKey}", authKey);
        await context.AppUserAuthKey
            .Where(k => k.Key == authKey)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastAccessedAtUtc, DateTime.UtcNow));
    }
}
