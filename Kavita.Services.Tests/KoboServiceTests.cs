using System.IO.Abstractions;
using System.Text.Json.Nodes;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class KoboServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private static SettingsService CreateSettingsService(IUnitOfWork unitOfWork)
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        return new SettingsService(unitOfWork, ds, Substitute.For<ILibraryWatcher>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SettingsService>>(),
            Substitute.For<IOidcService>(), Substitute.For<ILoggingService>());
    }

    private static KoboService CreateKoboService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        return new KoboService(unitOfWork, Substitute.For<IAuthKeyService>(), Substitute.For<IEventHub>(), mapper);
    }

    private static async Task<AppUser> SeedUser(IUnitOfWork unitOfWork, DataContext context)
    {
        var library = new LibraryBuilder("Kobo Lib").WithAllowKoboSync(true).Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        context.AppUser.Add(new AppUserBuilder("kobouser", "kobouser@localhost")
            .WithLibrary(library)
            .WithLocale("en")
            .WithRole(PolicyConstants.LoginRole)
            .Build());
        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.AuthKeys);
        Assert.NotNull(user);
        return user;
    }

    private static async Task ConfigureKoboSettings(IUnitOfWork unitOfWork, string hostName,
        bool enableKoboSync = true, string baseUrl = "/", int convertBudget = 30)
    {
        var settingsService = CreateSettingsService(unitOfWork);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.HostName = hostName;
        settings.BaseUrl = baseUrl;
        settings.EnableKoboSync = enableKoboSync;
        settings.KoboConvertTimeBudgetSeconds = convertBudget;
        await settingsService.UpdateSettings(settings);
    }

    [Fact]
    public async Task UpdateSettings_EnableKoboSync_Throws_WhenHostNameEmpty()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var settingsService = CreateSettingsService(unitOfWork);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.False(settings.EnableKoboSync);
        Assert.Equal(30, settings.KoboConvertTimeBudgetSeconds);

        settings.HostName = string.Empty;
        settings.EnableKoboSync = true;

        var ex = await Assert.ThrowsAsync<KavitaException>(() => settingsService.UpdateSettings(settings));
        Assert.Equal("kobo-hostname-required", ex.Message);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.False(reloaded.EnableKoboSync);
    }

    [Fact]
    public async Task UpdateSettings_EnableKoboSync_Persists_WhenHostNameSet()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var settingsService = CreateSettingsService(unitOfWork);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.HostName = "https://kavita.example.com";
        settings.EnableKoboSync = true;
        settings.KoboConvertTimeBudgetSeconds = 45;

        var updated = await settingsService.UpdateSettings(settings);

        Assert.True(updated.EnableKoboSync);
        Assert.Equal("https://kavita.example.com", updated.HostName);
        Assert.Equal(45, updated.KoboConvertTimeBudgetSeconds);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.True(reloaded.EnableKoboSync);
        Assert.Equal(45, reloaded.KoboConvertTimeBudgetSeconds);
    }

    [Fact]
    public async Task GetOrCreateSyncUrl_Throws_WhenFeatureDisabled()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", enableKoboSync: false);

        var koboService = CreateKoboService(unitOfWork, mapper);
        var ex = await Assert.ThrowsAsync<KavitaException>(() => koboService.GetOrCreateSyncUrlAsync(user.Id));
        Assert.Equal("kobo-sync-disabled", ex.Message);
    }

    [Fact]
    public async Task GetOrCreateSyncUrl_Throws_WhenHostNameEmpty()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);

        // Enable without hostname is blocked; seed enable flag directly for this gate
        var enableSetting = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.EnableKoboSync);
        enableSetting.Value = "true";
        await context.SaveChangesAsync();

        var koboService = CreateKoboService(unitOfWork, mapper);
        var ex = await Assert.ThrowsAsync<KavitaException>(() => koboService.GetOrCreateSyncUrlAsync(user.Id));
        Assert.Equal("kobo-hostname-required", ex.Message);
    }

    [Fact]
    public async Task GetOrCreateSyncUrl_LazyMintsKoboAuthKey_AndUsesHostNameBaseUrl()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", baseUrl: "/kavita/");

        Assert.DoesNotContain(user.AuthKeys, k => k.Name == AuthKeyHelper.KoboKeyName);

        var koboService = CreateKoboService(unitOfWork, mapper);
        var url = await koboService.GetOrCreateSyncUrlAsync(user.Id);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.AuthKeys);
        Assert.NotNull(reloaded);
        var koboKey = Assert.Single(reloaded.AuthKeys, k => k.Name == AuthKeyHelper.KoboKeyName);
        Assert.Equal(AuthKeyProvider.User, koboKey.Provider);
        Assert.Equal($"https://kavita.example.com/kavita/api/kobo/{koboKey.Key}", url);

        // Second call returns same URL without minting another key
        var urlAgain = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        Assert.Equal(url, urlAgain);
        reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.AuthKeys);
        Assert.Single(reloaded!.AuthKeys, k => k.Name == AuthKeyHelper.KoboKeyName);
    }

    [Fact]
    public async Task RotateSyncAuthKey_InvalidatesPreviousUrl()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");

        var koboService = CreateKoboService(unitOfWork, mapper);
        var originalUrl = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        var originalKey = (await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.AuthKeys))!
            .AuthKeys.Single(k => k.Name == AuthKeyHelper.KoboKeyName).Key;

        var rotatedUrl = await koboService.RotateSyncAuthKeyAsync(user.Id);
        Assert.NotEqual(originalUrl, rotatedUrl);
        Assert.StartsWith("https://kavita.example.com/api/kobo/", rotatedUrl);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.AuthKeys);
        var newKey = Assert.Single(reloaded!.AuthKeys, k => k.Name == AuthKeyHelper.KoboKeyName);
        Assert.NotEqual(originalKey, newKey.Key);
        Assert.Equal(rotatedUrl, $"https://kavita.example.com/api/kobo/{newKey.Key}");
        Assert.DoesNotContain(originalKey, rotatedUrl);
    }

    [Fact]
    public async Task RevokeSyncAuthKey_RemovesKey_UntilCreateViewAgain()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");

        var koboService = CreateKoboService(unitOfWork, mapper);
        var firstUrl = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        await koboService.RevokeSyncAuthKeyAsync(user.Id);

        var afterRevoke = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.AuthKeys);
        Assert.DoesNotContain(afterRevoke!.AuthKeys, k => k.Name == AuthKeyHelper.KoboKeyName);

        var ex = await Assert.ThrowsAsync<KavitaException>(() => koboService.RotateSyncAuthKeyAsync(user.Id));
        Assert.Equal("kobo-sync-key-missing", ex.Message);

        var remintedUrl = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        Assert.NotEqual(firstUrl, remintedUrl);
        var afterRemint = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.AuthKeys);
        Assert.Contains(afterRemint!.AuthKeys, k => k.Name == AuthKeyHelper.KoboKeyName);
    }

    [Fact]
    public async Task Library_AllowKoboSync_DefaultsFalse_AndCanBeEnabled()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("Opt In").Build();
        Assert.False(library.AllowKoboSync);

        library.AllowKoboSync = true;
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var reloaded = await context.Library.AsNoTracking().SingleAsync(l => l.Name == "Opt In");
        Assert.True(reloaded.AllowKoboSync);
    }

    [Fact]
    public void BuildSyncUrl_ComposesHostNameAndBaseUrl()
    {
        Assert.Equal("https://example.com/api/kobo/abc123",
            KoboService.BuildSyncUrl("https://example.com", "/", "abc123"));
        Assert.Equal("https://example.com/base/api/kobo/abc123",
            KoboService.BuildSyncUrl("https://example.com/", "/base/", "abc123"));
    }

    [Fact]
    public async Task ResolveUserId_Rejects_InvalidMissingAndRevokedTokens()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);

        await Assert.ThrowsAsync<KavitaUnauthenticatedUserException>(() =>
            koboService.ResolveUserIdAsync(string.Empty));
        await Assert.ThrowsAsync<KavitaUnauthenticatedUserException>(() =>
            koboService.ResolveUserIdAsync("not-a-real-token"));

        var url = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        var token = url[(url.LastIndexOf('/') + 1)..];
        Assert.Equal(user.Id, await koboService.ResolveUserIdAsync(token));

        // OPDS key must not authenticate as a Kobo sync token
        unitOfWork.UserRepository.Add(new AppUserAuthKey
        {
            Name = AuthKeyHelper.OpdsKeyName,
            Key = "opdskey12",
            AppUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Provider = AuthKeyProvider.User,
        });
        await unitOfWork.CommitAsync();
        await Assert.ThrowsAsync<KavitaUnauthenticatedUserException>(() =>
            koboService.ResolveUserIdAsync("opdskey12"));

        await koboService.RevokeSyncAuthKeyAsync(user.Id);
        await Assert.ThrowsAsync<KavitaUnauthenticatedUserException>(() =>
            koboService.ResolveUserIdAsync(token));
    }

    [Fact]
    public async Task ResolveUserId_Rejects_WhenFeatureDisabled()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", enableKoboSync: false);

        await Assert.ThrowsAsync<KavitaUnauthenticatedUserException>(() =>
            koboService.ResolveUserIdAsync(token));
    }

    [Fact]
    public async Task GetInitialization_RewritesResources_FromHostNameAndBaseUrl()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", baseUrl: "/kavita/");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var result = await koboService.GetInitializationAsync(token);
        var resources = result.Resources;
        var tokenBase = $"https://kavita.example.com/kavita/api/kobo/{token}";

        Assert.Equal("https://kavita.example.com/kavita", resources["image_host"]!.GetValue<string>());
        Assert.Equal($"{tokenBase}/{{ImageId}}/{{width}}/{{height}}/false/image.jpg",
            resources["image_url_template"]!.GetValue<string>());
        Assert.Equal(
            $"{tokenBase}/{{ImageId}}/{{width}}/{{height}}/{{Quality}}/{{IsGreyscale}}/image.jpg",
            resources["image_url_quality_template"]!.GetValue<string>());
        Assert.Equal($"{tokenBase}/v1/library/sync", resources["library_sync"]!.GetValue<string>());
        Assert.Equal(KoboInitializationResult.ApiTokenHeaderValue, "e30=");
        // Native map still present (not request-host based)
        Assert.False(string.IsNullOrEmpty(resources["device_auth"]?.GetValue<string>()));
        Assert.DoesNotContain("127.0.0.1", resources["image_host"]!.GetValue<string>());
    }

    [Fact]
    public async Task CreateDeviceAuthResponse_ReturnsDummyBearerTokens()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var auth = await koboService.CreateDeviceAuthResponseAsync(token, "device-user-key");

        Assert.Equal("Bearer", auth.TokenType);
        Assert.Equal("device-user-key", auth.UserKey);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(Guid.TryParse(auth.TrackingId, out _));
    }

    [Fact]
    public async Task KeepAliveStubs_MatchCalibreWebKnownBodies()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        _ = await koboService.GetOrCreateSyncUrlAsync(user.Id);

        var empty = Assert.IsType<JsonObject>(koboService.GetEmptyStub());
        Assert.Empty(empty);

        var benefits = Assert.IsType<JsonObject>(koboService.GetLoyaltyBenefitsStub());
        Assert.NotNull(benefits["Benefits"]?.AsObject());
        Assert.Empty(benefits["Benefits"]!.AsObject());

        var tests = Assert.IsType<JsonObject>(koboService.GetAnalyticsTestsStub("abc"));
        Assert.Equal("Success", tests["Result"]!.GetValue<string>());
        Assert.Equal("abc", tests["TestKey"]!.GetValue<string>());
        Assert.Empty(tests["Tests"]!.AsObject());
    }

    [Fact]
    public async Task ReadingStateStubs_AcknowledgeWithoutProgressSideEffects()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        _ = await koboService.GetOrCreateSyncUrlAsync(user.Id);

        const string entitlementId = "11111111-1111-1111-1111-111111111111";
        var getState = Assert.IsType<JsonArray>(koboService.GetReadingStateStub(entitlementId));
        Assert.Equal(entitlementId, getState[0]!["EntitlementId"]!.GetValue<string>());
        Assert.Equal("ReadyToRead", getState[0]!["StatusInfo"]!["Status"]!.GetValue<string>());

        var putState = Assert.IsType<JsonObject>(koboService.PutReadingStateStub(entitlementId));
        Assert.Equal("Success", putState["RequestResult"]!.GetValue<string>());
        Assert.Equal(entitlementId,
            putState["UpdateResults"]![0]!["EntitlementId"]!.GetValue<string>());

        // No progress / reading-history rows written by ACK stubs
        Assert.Empty(context.AppUserProgresses);
    }
}
