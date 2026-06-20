using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

public class OAuthService(
    ILogger<OAuthService> logger,
    IUnitOfWork unitOfWork,
    IScrobblingService scrobblingService,
    IKavitaPlusApiService kavitaPlusApiService,
    IEventHub eventHub,
    IKavitaPlusAuditService kavitaPlusAuditService): IOAuthService
{
    private const string DiscordMeApiUrl = "https://discord.com/api/users/@me";

    public async Task HandleCallback(AppUser user, OAuthUpstream upstream, string token, string? refreshToken = null)
    {
        logger.LogDebug("Handling callback {Callback}, HasToken: {HasToken}, HasRefreshToken: {HasRefreshToken}",
            upstream.ToString(), !string.IsNullOrEmpty(token), !string.IsNullOrEmpty(refreshToken));
        switch (upstream)
        {
            case OAuthUpstream.Discord:
                await SetDiscordId(user, token);
                return;
            case OAuthUpstream.MangaBaka:
                await SetScrobbleProviderToken(user, ScrobbleProvider.MangaBaka, token, refreshToken);
                return;
            case OAuthUpstream.AniList:
                await SetScrobbleProviderToken(user, ScrobbleProvider.AniList, token);
                return;
            case OAuthUpstream.MyAnimeList:
                await SetScrobbleProviderToken(user, ScrobbleProvider.Mal, token, refreshToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(upstream), upstream, null);
        }
    }

    private async Task SetDiscordId(AppUser user, string token)
    {
        if (!await unitOfWork.UserRepository.IsUserAdminAsync(user))
        {
            return;
        }

        try
        {
            var response = await DiscordMeApiUrl
                .WithOAuthBearerToken(token)
                .GetJsonAsync<JsonElement>();

            var snowflake = response.GetProperty("id").GetString();
            var username = response.GetProperty("username").GetString();

            if (string.IsNullOrEmpty(snowflake))
            {
                logger.LogWarning("Cannot link discord, as no id was found");
                return;
            }

            logger.LogDebug("Will be linking the discord user {DiscordId} to K+ license on behalf of {UserId}",
                snowflake, user.Id);

            BackgroundJob.Enqueue<ILicenseService>(s => s.LinkDiscord(snowflake, username ?? string.Empty, CancellationToken.None));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get discord user id, cannot update id on K+");
        }
    }

    private async Task SetScrobbleProviderToken(AppUser user, ScrobbleProvider provider, string token, string? refreshToken = null)
    {
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
        user.ScrobbleProviders[provider].RefreshToken = refreshToken ?? string.Empty;
        unitOfWork.UserRepository.Update(user);

        await unitOfWork.CommitAsync();

        BackgroundJob.Enqueue(() => SyncWithDelay(user.Id, provider, CancellationToken.None));
    }

    /// <summary>
    /// Wrapper around <see cref="IScrobblingService.SyncProviderInfo"/> to avoid an issue with EF.Core's concurrency handling.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="provider"></param>
    /// <param name="ct"></param>
    public async Task SyncWithDelay(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        await scrobblingService.SyncProviderInfo(userId, provider, ct);
    }

    public async Task RefreshTokens(CancellationToken ct = default)
    {
        var users = await unitOfWork.UserRepository.GetAllUsersAsync(ct: ct);

        foreach (var user in users)
        {
            foreach (var provider in user.ScrobbleProviders.Keys)
            {
                var settings = user.ScrobbleProviders[provider];

                if (!provider.SupportsOAuthTokenRefresh()) continue;

                var upstream = provider.ToOAuthUpstream();
                if (upstream == null)
                {
                    logger.LogTrace("Skipping {Provider} as they could not be mapped to an OAuth upstream for automatic refresh", provider);
                    continue;
                }

                if (string.IsNullOrEmpty(settings.AuthenticationToken) || string.IsNullOrEmpty(settings.RefreshToken))
                {
                    logger.LogTrace("Skipping {Provider} for {UserId} as they do not have a valid token for automatic refresh", provider, user.Id);
                    continue;
                }

                var timeRemaining = settings.ValidUntilUtc - DateTime.UtcNow;

                if (timeRemaining > TimeSpan.FromDays(1))
                {
                    logger.LogTrace("Skipping {Provider} for {UserId} as they have a token that is still valid for {TimeRemaining}", provider, user.Id, timeRemaining);
                    continue;
                }

                logger.LogDebug("Token for {Provider} for user {UserId} is about to expire, refreshing", provider, user.Id);

                var response = await kavitaPlusApiService.RefreshToken(new RefreshTokenRequestDto()
                {
                    RefreshToken = settings.RefreshToken,
                    Upstream = upstream.Value,
                }, ct);

                if (!response.IsSuccess)
                {
                    logger.LogWarning("Failed to refresh token for {Provider} for user {UserId}: {ErrorMessage}", provider, user.Id, response.ErrorMessage);

                    await kavitaPlusAuditService.LogAsync(KavitaPlusAuditCategory.System,
                        KavitaPlusEventType.SystemTokenRefresh, AuditStatus.Failure,
                        payload: new AuditLogSystemTokenRefreshParamsDto
                        {
                            Provider = provider
                        }, userId: user.Id, ct: ct);

                    continue;
                }

                settings.AuthenticationToken = response.Data.AccessToken;
                settings.RefreshToken = response.Data.RefreshToken;
                // Remove 30s for latency
                settings.ValidUntilUtc = DateTime.UtcNow
                                         + TimeSpan.FromSeconds(response.Data.ExpiresIn)
                                         - TimeSpan.FromSeconds(30);
                settings.LastSyncedUtc = DateTime.UtcNow;

                await kavitaPlusAuditService.LogAsync(KavitaPlusAuditCategory.System,
                    KavitaPlusEventType.SystemTokenRefresh, AuditStatus.Success,
                    payload: new AuditLogSystemTokenRefreshParamsDto
                    {
                        Provider = provider,
                        ValidUntilUtc = settings.ValidUntilUtc,
                    }, error: response.ErrorMessage, userId: user.Id, ct: ct);

                unitOfWork.UserRepository.Update(user);
                await unitOfWork.CommitAsync(ct);

                await eventHub.SendMessageToAsync(MessageFactory.ScrobbleProviderUpdated,
                    MessageFactory.ScrobbleProviderUpdatedEvent(provider), user.Id, ct);
            }
        }
    }
}
