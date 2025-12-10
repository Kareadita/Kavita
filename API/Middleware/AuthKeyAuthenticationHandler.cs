using System;
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
            var cacheKey = $"authKey_{apiKey}";
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

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("AuthType", nameof(AuthenticationType.AuthKey))
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Auth Key authentication failed");
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

        // Check OPDS path: /api/opds/{apiKey}/...
        var path = request.Path.Value ?? string.Empty;
        if (path.Contains("/api/opds/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var opdsIndex = Array.FindIndex(segments, s =>
                s.Equals("opds", StringComparison.OrdinalIgnoreCase));

            if (opdsIndex >= 0 && opdsIndex + 1 < segments.Length)
            {
                return segments[opdsIndex + 1];
            }
        }

        // Check if embedded in route parameters (e.g., /api/somepath/{apiKey}/other)
        if (request.RouteValues.TryGetValue("apiKey", out var routeKey))
        {
            return routeKey?.ToString();
        }

        return null;
    }
}
