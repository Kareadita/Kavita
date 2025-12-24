using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities.Progress;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Middleware;
#nullable enable

public class AuthKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "AuthKey";
}

public class AuthKeyAuthenticationHandler : AuthenticationHandler<AuthKeyAuthenticationOptions>
{
private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(15)
    };

    public AuthKeyAuthenticationHandler(
        IOptionsMonitor<AuthKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUnitOfWork unitOfWork,
        HybridCache cache)
        : base(options, logger, encoder)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = ExtractAuthKey(Request);

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var cacheKey = CreateCacheKey(apiKey);
            var user = await _cache.GetOrCreateAsync(
                cacheKey,
                (apiKey, _unitOfWork),
                static async (state, cancel) =>
                    await state._unitOfWork.UserRepository.GetUserDtoByAuthKeyAsync(state.apiKey),
                CacheOptions,
                cancellationToken: Context.RequestAborted).ConfigureAwait(false);

            if (user?.Id == null || string.IsNullOrEmpty(user.Username))
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            var claims = new List<Claim>()
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Name, user.Username),
                new("AuthType", nameof(AuthenticationType.AuthKey))
            };

            if (user.Roles != null && user.Roles.Any())
            {
                claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Auth Key authentication failed");
            return AuthenticateResult.Fail("Auth Key authentication failed");
        }
    }

    public static string? ExtractAuthKey(HttpRequest request)
    {
        // Check query string
        if (request.Query.TryGetValue("apiKey", out var apiKeyQuery))
        {
            return apiKeyQuery.ToString();
        }

        // Check header
        if (request.Headers.TryGetValue(Headers.ApiKey, out var authHeader))
        {
            return authHeader.ToString();
        }

        // Check if embedded in route parameters (e.g., /api/somepath/{apiKey}/other)
        if (request.RouteValues.TryGetValue("apiKey", out var routeKey))
        {
            return routeKey?.ToString();
        }

        return null;
    }

    public static string CreateCacheKey(string keyValue)
    {
        return $"authKey_{keyValue}";
    }
}
