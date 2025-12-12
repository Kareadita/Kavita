using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;

namespace API.Services.Store;

/// <summary>
/// The <see cref="ITicketStore"/> is used as <see cref="CookieAuthenticationOptions.SessionStore"/> for the OIDC implementation
/// The full AuthenticationTicket cannot be included in the Cookie as popular reverse proxies (like nginx) will deny the request
/// due the large header size. Instead, the key is used.
/// </summary>
/// <param name="cache"></param>
public class CustomTicketStore(IDistributedCache cache, TicketSerializer ticketSerializer): ITicketStore
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
        var options = new DistributedCacheEntryOptions();

        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
        {
            options.AbsoluteExpiration = expiresUtc.Value;
        }
        else
        {
            options.SlidingExpiration = TimeSpan.FromDays(7);
        }

        return cache.SetAsync(key, ticketSerializer.Serialize(ticket), options);
    }

    public async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        var bytes = await cache.GetAsync(key);
        if (bytes == null) return CreateFailureTicket();

        return ticketSerializer.Deserialize(bytes);
    }

    public Task RemoveAsync(string key)
    {
        return cache.RemoveAsync(key);
    }

    private static AuthenticationTicket CreateFailureTicket()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1), // Already expired
        };

        return new AuthenticationTicket(principal, properties, "Cookies");
    }
}
