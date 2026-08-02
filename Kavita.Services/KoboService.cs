using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Helpers;
using Configuration = Kavita.Common.Configuration;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.User;
using Kavita.Services.Kobo;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services;

public class KoboService(
    IUnitOfWork unitOfWork,
    IAuthKeyService authKeyService,
    IEventHub eventHub,
    IMapper mapper)
    : IKoboService
{
    public const string SyncPathPrefix = "api/kobo/";

    public async Task<string> GetOrCreateSyncUrlAsync(int userId, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnsureSyncUrlAvailable(settings.EnableKoboSync, settings.HostName);

        var authKey = await GetOrCreateKoboAuthKeyAsync(userId, ct);
        return BuildSyncUrl(settings.HostName, settings.BaseUrl, authKey.Key);
    }

    public async Task<string> RotateSyncAuthKeyAsync(int userId, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnsureSyncUrlAvailable(settings.EnableKoboSync, settings.HostName);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.AuthKeys, ct);
        if (user == null) throw new KavitaException("access-denied");

        var authKey = user.AuthKeys.FirstOrDefault(k =>
            string.Equals(k.Name, AuthKeyHelper.KoboKeyName, StringComparison.Ordinal));
        if (authKey == null) throw new KavitaException("kobo-sync-key-missing");

        var oldKeyValue = authKey.Key;
        if (authKey.ExpiresAtUtc != null)
        {
            var originalDuration = authKey.ExpiresAtUtc.Value - authKey.CreatedAtUtc;
            authKey.ExpiresAtUtc = DateTime.UtcNow.Add(originalDuration);
        }

        authKey.Key = AuthKeyHelper.GenerateKey();
        await unitOfWork.CommitAsync(ct);
        await authKeyService.InvalidateAsync(oldKeyValue, ct);

        var dto = mapper.Map<AuthKeyDto>(authKey);
        await eventHub.SendMessageToAsync(MessageFactory.AuthKeyUpdate, MessageFactory.AuthKeyUpdatedEvent(dto),
            userId, ct);

        return BuildSyncUrl(settings.HostName, settings.BaseUrl, authKey.Key);
    }

    public async Task RevokeSyncAuthKeyAsync(int userId, CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.AuthKeys, ct);
        if (user == null) throw new KavitaException("access-denied");

        var authKey = user.AuthKeys.FirstOrDefault(k =>
            string.Equals(k.Name, AuthKeyHelper.KoboKeyName, StringComparison.Ordinal));
        if (authKey == null) throw new KavitaException("kobo-sync-key-missing");

        var oldKeyValue = authKey.Key;
        var authKeyId = authKey.Id;
        unitOfWork.UserRepository.Delete(authKey);
        await unitOfWork.CommitAsync(ct);

        await authKeyService.InvalidateAsync(oldKeyValue, ct);
        await eventHub.SendMessageToAsync(MessageFactory.AuthKeyDeleted, MessageFactory.AuthKeyDeletedEvent(authKeyId),
            userId, ct);
    }

    public async Task<int> ResolveUserIdAsync(string authToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authToken))
        {
            throw new KavitaUnauthenticatedUserException("kobo-auth-invalid");
        }

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKoboSync)
        {
            throw new KavitaUnauthenticatedUserException("kobo-sync-disabled");
        }

        var userId = await unitOfWork.DataContext.AppUserAuthKey
            .Where(k => k.Key == authToken && k.Name == AuthKeyHelper.KoboKeyName)
            .HasNotExpired()
            .Select(k => k.AppUserId)
            .FirstOrDefaultAsync(ct);

        if (userId <= 0)
        {
            throw new KavitaUnauthenticatedUserException("kobo-auth-invalid");
        }

        return userId;
    }

    public async Task<KoboInitializationResult> GetInitializationAsync(string authToken,
        CancellationToken ct = default)
    {
        await ResolveUserIdAsync(authToken, ct);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.HostName))
        {
            throw new KavitaException("kobo-hostname-required");
        }

        var publicBase = BuildPublicBase(settings.HostName, settings.BaseUrl);
        var tokenBase = $"{publicBase}/{SyncPathPrefix}{authToken}";

        var resources = NativeKoboResources.CreateCopy();
        resources["image_host"] = publicBase;
        resources["image_url_template"] =
            $"{tokenBase}/{{ImageId}}/{{width}}/{{height}}/false/image.jpg";
        resources["image_url_quality_template"] =
            $"{tokenBase}/{{ImageId}}/{{width}}/{{height}}/{{Quality}}/{{IsGreyscale}}/image.jpg";
        resources["library_sync"] = $"{tokenBase}/v1/library/sync";

        return new KoboInitializationResult { Resources = resources };
    }

    public async Task<KoboAuthTokenDto> CreateDeviceAuthResponseAsync(string authToken, string? userKey,
        CancellationToken ct = default)
    {
        await ResolveUserIdAsync(authToken, ct);

        return new KoboAuthTokenDto
        {
            AccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            TokenType = "Bearer",
            TrackingId = Guid.NewGuid().ToString(),
            UserKey = userKey ?? string.Empty,
        };
    }

    public object GetEmptyStub() => new JsonObject();

    public object GetLoyaltyBenefitsStub() => new JsonObject
    {
        ["Benefits"] = new JsonObject(),
    };

    public object GetAnalyticsTestsStub(string? koboUserKey) => new JsonObject
    {
        ["Result"] = "Success",
        ["TestKey"] = koboUserKey ?? string.Empty,
        ["Tests"] = new JsonObject(),
    };

    public object GetReadingStateStub(string entitlementId)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return new JsonArray
        {
            new JsonObject
            {
                ["EntitlementId"] = entitlementId,
                ["Created"] = now,
                ["LastModified"] = now,
                ["PriorityTimestamp"] = now,
                ["StatusInfo"] = new JsonObject
                {
                    ["LastModified"] = now,
                    ["Status"] = "ReadyToRead",
                    ["TimesStartedReading"] = 0,
                },
                ["Statistics"] = new JsonObject
                {
                    ["LastModified"] = now,
                },
                ["CurrentBookmark"] = new JsonObject
                {
                    ["LastModified"] = now,
                },
            },
        };
    }

    public object PutReadingStateStub(string entitlementId)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return new JsonObject
        {
            ["RequestResult"] = "Success",
            ["UpdateResults"] = new JsonArray
            {
                new JsonObject
                {
                    ["EntitlementId"] = entitlementId,
                    ["LastModified"] = now,
                    ["PriorityTimestamp"] = now,
                    ["CurrentBookmarkResult"] = new JsonObject { ["Result"] = "Success" },
                    ["StatisticsResult"] = new JsonObject { ["Result"] = "Success" },
                    ["StatusInfoResult"] = new JsonObject { ["Result"] = "Success" },
                },
            },
        };
    }

    private async Task<AppUserAuthKey> GetOrCreateKoboAuthKeyAsync(int userId, CancellationToken ct)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.AuthKeys, ct);
        if (user == null) throw new KavitaException("access-denied");

        var existing = user.AuthKeys.FirstOrDefault(k =>
            string.Equals(k.Name, AuthKeyHelper.KoboKeyName, StringComparison.Ordinal));
        if (existing != null) return existing;

        var newKey = new AppUserAuthKey
        {
            Name = AuthKeyHelper.KoboKeyName,
            Key = AuthKeyHelper.GenerateKey(),
            AppUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Provider = AuthKeyProvider.User,
        };
        unitOfWork.UserRepository.Add(newKey);
        await unitOfWork.CommitAsync(ct);

        var dto = mapper.Map<AuthKeyDto>(newKey);
        await eventHub.SendMessageToAsync(MessageFactory.AuthKeyUpdate, MessageFactory.AuthKeyUpdatedEvent(dto),
            userId, ct);

        return newKey;
    }

    private static void EnsureSyncUrlAvailable(bool enableKoboSync, string? hostName)
    {
        if (!enableKoboSync) throw new KavitaException("kobo-sync-disabled");
        if (string.IsNullOrWhiteSpace(hostName)) throw new KavitaException("kobo-hostname-required");
    }

    /// <summary>
    /// Compose public sync URL from configured HostName + BaseUrl only (no request-host fallback).
    /// </summary>
    public static string BuildSyncUrl(string hostName, string baseUrl, string authKey)
    {
        return $"{BuildPublicBase(hostName, baseUrl)}/{SyncPathPrefix}{authKey}";
    }

    /// <summary>
    /// Public origin + optional BaseUrl path prefix (no trailing slash).
    /// </summary>
    public static string BuildPublicBase(string hostName, string baseUrl)
    {
        var origin = UrlHelper.RemoveEndingSlash(hostName.Trim());
        var pathBase = string.Empty;
        if (!string.IsNullOrEmpty(baseUrl) && !baseUrl.Equals(Configuration.DefaultBaseUrl))
        {
            pathBase = baseUrl;
            if (pathBase.EndsWith('/'))
            {
                pathBase = pathBase[..^1];
            }

            if (!pathBase.StartsWith('/'))
            {
                pathBase = "/" + pathBase;
            }
        }

        return $"{origin}{pathBase}";
    }
}
