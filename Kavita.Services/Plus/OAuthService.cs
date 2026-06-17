using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.API.Store;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

public class OAuthService(ILogger<OAuthService> logger, IUserContext userContext, IUnitOfWork unitOfWork, IScrobblingService scrobblingService): IOAuthService
{
    private const string DiscordMeApiUrl = "https://discord.com/api/users/@me";

    public async Task HandleCallback(OAuthUpstream upstream, string token, string? refreshToken = null)
    {
        logger.LogDebug("Handling callback {Callback}, HasToken: {HasToken}, HasRefreshToken: {HasRefreshToken}",
            upstream.ToString(), !string.IsNullOrEmpty(token), !string.IsNullOrEmpty(refreshToken));
        switch (upstream)
        {
            case OAuthUpstream.Discord:
                await SetDiscordId(token);
                return;
            case OAuthUpstream.MangaBaka:
                await SetScrobbleProviderToken(ScrobbleProvider.MangaBaka, token, refreshToken);
                return;
            case OAuthUpstream.AniList:
                await SetScrobbleProviderToken(ScrobbleProvider.AniList, token);
                return;
            case OAuthUpstream.MyAnimeList:
                await SetScrobbleProviderToken(ScrobbleProvider.Mal, token, refreshToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(upstream), upstream, null);
        }
    }

    private async Task SetDiscordId(string token)
    {
        if (!userContext.HasRole(PolicyConstants.AdminRole))
        {
            return;
        }

        var response = await DiscordMeApiUrl
            .WithOAuthBearerToken(token)
            .GetJsonAsync<JsonElement>();

        var snowflake = response.GetProperty("id").GetInt32();

        logger.LogDebug("Will be linking the discord user {DiscordId} to K+ license on behalf of {UserId}",
            snowflake, userContext.GetUserIdOrThrow());

        // TODO: Update license with this id, idk how
    }

    private async Task SetScrobbleProviderToken(ScrobbleProvider provider, string token, string? refreshToken = null)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userContext.GetUserIdOrThrow());
        if (user == null)
        {
            return;
        }

        var scrobbleSettings = user.ScrobbleProviders[provider];

        if (scrobbleSettings.AuthenticationToken == token && refreshToken == null)
        {
            return;
        }

        if (scrobbleSettings.AuthenticationToken == token && scrobbleSettings.RefreshToken == refreshToken)
        {
            return;
        }

        user.ScrobbleProviders[provider].AuthenticationToken = token;
        user.ScrobbleProviders[provider].RefreshToken = refreshToken;
        unitOfWork.UserRepository.Update(user);

        await unitOfWork.CommitAsync();

        BackgroundJob.Enqueue(() => SyncWithDelay(user.Id, provider, CancellationToken.None));
    }

    /// <summary>
    /// Wrapper around <see cref="IScrobblingService.SyncProviderInfo"/> to avoid an issue with EF.Core's concurrency handling.
    /// See https://github.com/dotnet/efcore/issues/24279
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="provider"></param>
    /// <param name="ct"></param>
    public async Task SyncWithDelay(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        await scrobblingService.SyncProviderInfo(userId, provider, ct);
    }

    public Task RefreshTokens()
    {
        throw new System.NotImplementedException();
    }
}
