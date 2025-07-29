using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace API.Services.Store;

public class CustomTicketStore(IMemoryCache cache): ITicketStore
{

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        // Note: It might not be needed to make this cryptographic random, but better safe than sorry
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var key = Convert.ToBase64String(bytes);

        await RenewAsync(key, ticket);

        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove,
            Size = 1,
        };

        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
        {
            options.AbsoluteExpiration = expiresUtc.Value;
        }
        else
        {
            options.SlidingExpiration = TimeSpan.FromDays(7);
        }

        cache.Set(key, ticket, options);

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        return Task.FromResult(cache.Get<AuthenticationTicket>(key));
    }

    public Task RemoveAsync(string key)
    {
        cache.Remove(key);

        return Task.CompletedTask;
    }
}
