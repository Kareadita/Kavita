using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
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
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Kavita.Services.ReadingLists;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class KoboServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private readonly string _epubPath = Path.Join(Directory.GetCurrentDirectory(), "Data", "AesopsFables.epub");
    private readonly string _cbzPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(),
        "../../../Test Data/ArchiveService/CoverImages/v10.cbz"));

    private static SettingsService CreateSettingsService(IUnitOfWork unitOfWork)
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        return new SettingsService(unitOfWork, ds, Substitute.For<ILibraryWatcher>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SettingsService>>(),
            Substitute.For<IOidcService>(), Substitute.For<ILoggingService>());
    }

    private static KoboService CreateKoboService(IUnitOfWork unitOfWork, IMapper mapper,
        string? coverImageDirectory = null, IKoboConversionService? conversionService = null)
    {
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.CoverImageDirectory.Returns(coverImageDirectory ?? Path.GetTempPath());
        return new KoboService(unitOfWork, Substitute.For<IAuthKeyService>(), Substitute.For<IEventHub>(), mapper,
            directoryService, new DownloadService(),
            conversionService ?? Substitute.For<IKoboConversionService>());
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
        bool enableKoboSync = true, string baseUrl = "/", int convertBudget = 30,
        int syncPageSize = KoboService.DefaultSyncPageSize, bool enableKepubConversion = false,
        string kepubifyPath = "")
    {
        var settingsService = CreateSettingsService(unitOfWork);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.HostName = hostName;
        settings.BaseUrl = baseUrl;
        settings.EnableKoboSync = enableKoboSync;
        settings.KoboConvertTimeBudgetSeconds = convertBudget;
        settings.KoboSyncPageSize = syncPageSize;
        settings.EnableKepubConversion = enableKepubConversion;
        settings.KepubifyPath = kepubifyPath;
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
        Assert.Equal(KoboService.DefaultSyncPageSize, settings.KoboSyncPageSize);

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
        settings.KoboSyncPageSize = 50;

        var updated = await settingsService.UpdateSettings(settings);

        Assert.True(updated.EnableKoboSync);
        Assert.Equal("https://kavita.example.com", updated.HostName);
        Assert.Equal(45, updated.KoboConvertTimeBudgetSeconds);
        Assert.Equal(50, updated.KoboSyncPageSize);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.True(reloaded.EnableKoboSync);
        Assert.Equal(45, reloaded.KoboConvertTimeBudgetSeconds);
        Assert.Equal(50, reloaded.KoboSyncPageSize);
    }

    [Fact]
    public async Task UpdateSettings_KoboSyncPageSize_RejectsOutOfBounds()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var settingsService = CreateSettingsService(unitOfWork);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.KoboSyncPageSize = 0;

        var tooLow = await Assert.ThrowsAsync<KavitaException>(() => settingsService.UpdateSettings(settings));
        Assert.Equal("kobo-sync-page-size", tooLow.Message);

        settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.KoboSyncPageSize = KoboService.MaxSyncPageSize + 1;

        var tooHigh = await Assert.ThrowsAsync<KavitaException>(() => settingsService.UpdateSettings(settings));
        Assert.Equal("kobo-sync-page-size", tooHigh.Message);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.Equal(KoboService.DefaultSyncPageSize, reloaded.KoboSyncPageSize);
    }

    [Fact]
    public async Task UpdateSettings_EnableKepubConversion_RequiresKepubifyPath()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var settingsService = CreateSettingsService(unitOfWork);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.False(settings.EnableKepubConversion);
        Assert.Equal(string.Empty, settings.KepubifyPath);

        settings.EnableKepubConversion = true;
        settings.KepubifyPath = string.Empty;

        var ex = await Assert.ThrowsAsync<KavitaException>(() => settingsService.UpdateSettings(settings));
        Assert.Equal("kobo-kepubify-path-required", ex.Message);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.False(reloaded.EnableKepubConversion);
    }

    [Fact]
    public async Task UpdateSettings_EnableKepubConversion_Persists_WhenPathSet()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var settingsService = CreateSettingsService(unitOfWork);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.EnableKepubConversion = true;
        settings.KepubifyPath = "/usr/bin/kepubify";

        var updated = await settingsService.UpdateSettings(settings);

        Assert.True(updated.EnableKepubConversion);
        Assert.Equal("/usr/bin/kepubify", updated.KepubifyPath);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.True(reloaded.EnableKepubConversion);
        Assert.Equal("/usr/bin/kepubify", reloaded.KepubifyPath);
    }

    [Fact]
    public async Task UpdateSettings_KoboCacheMaxBytes_Persists_AndRejectsNegative()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var settingsService = CreateSettingsService(unitOfWork);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.Null(settings.KoboEpubCacheMaxBytes);
        Assert.Null(settings.KoboKepubCacheMaxBytes);

        settings.KoboEpubCacheMaxBytes = 1_048_576;
        settings.KoboKepubCacheMaxBytes = 2_097_152;
        var updated = await settingsService.UpdateSettings(settings);
        Assert.Equal(1_048_576, updated.KoboEpubCacheMaxBytes);
        Assert.Equal(2_097_152, updated.KoboKepubCacheMaxBytes);

        settings.KoboEpubCacheMaxBytes = null;
        settings.KoboKepubCacheMaxBytes = 0;
        await settingsService.UpdateSettings(settings);

        var reloaded = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        Assert.Null(reloaded.KoboEpubCacheMaxBytes);
        Assert.Null(reloaded.KoboKepubCacheMaxBytes);

        settings.KoboEpubCacheMaxBytes = -1;
        var ex = await Assert.ThrowsAsync<KavitaException>(() => settingsService.UpdateSettings(settings));
        Assert.Equal("kobo-epub-cache-max-bytes", ex.Message);
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
        Assert.Equal($"{tokenBase}/v1/library/tags", resources["tags"]!.GetValue<string>());
        Assert.Equal($"{tokenBase}/v1/library/tags/{{TagId}}/Items",
            resources["tag_items"]!.GetValue<string>());
        Assert.Equal($"{tokenBase}/v1/library/tags/{{TagId}}",
            resources["delete_tag"]!.GetValue<string>());
        Assert.Equal($"{tokenBase}/v1/library/tags/{{TagId}}/items/delete",
            resources["delete_tag_items"]!.GetValue<string>());
        Assert.Equal($"{tokenBase}/v1/library/tags/{{TagId}}",
            resources["rename_tag"]!.GetValue<string>());
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
    public async Task ReadingState_GetPut_MapsAppUserProgress_PersistsLocation_IgnoresStatistics()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Progress Book", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var entitlementId = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var emptyGet = Assert.IsType<JsonArray>(
            await koboService.GetReadingStateAsync(token, entitlementId));
        Assert.Equal("ReadyToRead", emptyGet[0]!["StatusInfo"]!["Status"]!.GetValue<string>());
        Assert.Null(emptyGet[0]!["CurrentBookmark"]!["ProgressPercent"]);
        Assert.Null(emptyGet[0]!["CurrentBookmark"]!["Location"]);

        var putBody = new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["CurrentBookmark"] = new JsonObject
                    {
                        ["ProgressPercent"] = 40,
                        ["Location"] = new JsonObject
                        {
                            ["Value"] = "kobo.1.2",
                            ["Type"] = "KoboSpan",
                            ["Source"] = "OEBPS/chapter1.xhtml",
                        },
                    },
                    ["Statistics"] = new JsonObject
                    {
                        ["SpentReadingMinutes"] = 12,
                        ["RemainingTimeMinutes"] = 30,
                    },
                    ["StatusInfo"] = new JsonObject
                    {
                        ["Status"] = "Reading",
                    },
                },
            },
        };

        var putState = Assert.IsType<JsonObject>(
            await koboService.PutReadingStateAsync(token, entitlementId, putBody));
        Assert.Equal("Success", putState["RequestResult"]!.GetValue<string>());
        Assert.NotNull(putState["UpdateResults"]![0]!["CurrentBookmarkResult"]);
        Assert.NotNull(putState["UpdateResults"]![0]!["StatisticsResult"]);
        Assert.NotNull(putState["UpdateResults"]![0]!["StatusInfoResult"]);

        var progress = Assert.Single(context.AppUserProgresses);
        Assert.Equal(4, progress.PagesRead); // 40% of 10 pages
        Assert.Null(progress.BookScrollId);

        var location = Assert.Single(context.AppUserKoboReadingLocation);
        Assert.Equal("kobo.1.2", location.LocationValue);
        Assert.Equal("KoboSpan", location.LocationType);
        Assert.Equal("OEBPS/chapter1.xhtml", location.LocationSource);

        var getState = Assert.IsType<JsonArray>(
            await koboService.GetReadingStateAsync(token, entitlementId));
        Assert.Equal("Reading", getState[0]!["StatusInfo"]!["Status"]!.GetValue<string>());
        Assert.Equal(40, getState[0]!["CurrentBookmark"]!["ProgressPercent"]!.GetValue<double>());
        var getLocation = Assert.IsType<JsonObject>(getState[0]!["CurrentBookmark"]!["Location"]);
        Assert.Equal("kobo.1.2", getLocation["Value"]!.GetValue<string>());
        Assert.Equal("KoboSpan", getLocation["Type"]!.GetValue<string>());
        Assert.Equal("OEBPS/chapter1.xhtml", getLocation["Source"]!.GetValue<string>());
        Assert.Null(getState[0]!["Statistics"]!["SpentReadingMinutes"]);
    }

    [Fact]
    public async Task ReadingState_Put_FalsyLocation_LeavesPriorLocation()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Falsy Location", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var entitlementId = KoboEntitlementId.FromChapterIdString(chapter.Id);

        await koboService.PutReadingStateAsync(token, entitlementId, new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["CurrentBookmark"] = new JsonObject
                    {
                        ["ProgressPercent"] = 40,
                        ["Location"] = new JsonObject
                        {
                            ["Value"] = "kobo.3.4",
                            ["Type"] = "KoboSpan",
                            ["Source"] = "OEBPS/c2.xhtml",
                        },
                    },
                    ["StatusInfo"] = new JsonObject { ["Status"] = "Reading" },
                },
            },
        });

        // Percent-only update (absent Location) must not wipe prior Location columns.
        await koboService.PutReadingStateAsync(token, entitlementId, new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["CurrentBookmark"] = new JsonObject { ["ProgressPercent"] = 60 },
                    ["StatusInfo"] = new JsonObject { ["Status"] = "Reading" },
                },
            },
        });

        var location = Assert.Single(context.AppUserKoboReadingLocation);
        Assert.Equal("kobo.3.4", location.LocationValue);
        Assert.Equal(6, Assert.Single(context.AppUserProgresses).PagesRead);

        // Explicit empty Value is falsy — also leave prior.
        await koboService.PutReadingStateAsync(token, entitlementId, new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["CurrentBookmark"] = new JsonObject
                    {
                        ["ProgressPercent"] = 70,
                        ["Location"] = new JsonObject
                        {
                            ["Value"] = "",
                            ["Type"] = "Ignored",
                            ["Source"] = "Ignored",
                        },
                    },
                    ["StatusInfo"] = new JsonObject { ["Status"] = "Reading" },
                },
            },
        });

        await context.Entry(location).ReloadAsync();
        Assert.Equal("kobo.3.4", location.LocationValue);
        Assert.Equal("KoboSpan", location.LocationType);
        Assert.Equal("OEBPS/c2.xhtml", location.LocationSource);
        Assert.Equal(7, Assert.Single(context.AppUserProgresses).PagesRead);
    }

    [Fact]
    public async Task ReadingState_Put_LastWriteWins_KeepsNewerServerProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "LWW Book", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var entitlementId = KoboEntitlementId.FromChapterIdString(chapter.Id);

        // Server progress at 80% with Location
        var serverPut = new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["CurrentBookmark"] = new JsonObject
                    {
                        ["ProgressPercent"] = 80,
                        ["Location"] = new JsonObject
                        {
                            ["Value"] = "kobo.8.1",
                            ["Type"] = "KoboSpan",
                            ["Source"] = "OEBPS/end.xhtml",
                        },
                    },
                    ["StatusInfo"] = new JsonObject { ["Status"] = "Reading" },
                },
            },
        };
        await koboService.PutReadingStateAsync(token, entitlementId, serverPut);
        var serverProgress = Assert.Single(context.AppUserProgresses);
        Assert.Equal(8, serverProgress.PagesRead);
        var serverTs = serverProgress.LastModifiedUtc;
        Assert.Equal("kobo.8.1", Assert.Single(context.AppUserKoboReadingLocation).LocationValue);

        // Older device timestamp must not overwrite percent or Location (shared LWW package)
        var stalePut = new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["LastModified"] = serverTs.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["CurrentBookmark"] = new JsonObject
                    {
                        ["LastModified"] = serverTs.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["ProgressPercent"] = 10,
                        ["Location"] = new JsonObject
                        {
                            ["Value"] = "kobo.stale",
                            ["Type"] = "KoboSpan",
                            ["Source"] = "OEBPS/stale.xhtml",
                        },
                    },
                    ["StatusInfo"] = new JsonObject { ["Status"] = "Reading" },
                },
            },
        };
        await koboService.PutReadingStateAsync(token, entitlementId, stalePut);
        await context.Entry(serverProgress).ReloadAsync();
        Assert.Equal(8, serverProgress.PagesRead);
        Assert.Equal("kobo.8.1", Assert.Single(context.AppUserKoboReadingLocation).LocationValue);

        // Newer device timestamp wins percent + Location together
        var freshPut = new JsonObject
        {
            ["ReadingStates"] = new JsonArray
            {
                new JsonObject
                {
                    ["LastModified"] = serverTs.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["CurrentBookmark"] = new JsonObject
                    {
                        ["LastModified"] = serverTs.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["ProgressPercent"] = 100,
                        ["Location"] = new JsonObject
                        {
                            ["Value"] = "kobo.10.1",
                            ["Type"] = "KoboSpan",
                            ["Source"] = "OEBPS/done.xhtml",
                        },
                    },
                    ["StatusInfo"] = new JsonObject { ["Status"] = "Finished" },
                },
            },
        };
        await koboService.PutReadingStateAsync(token, entitlementId, freshPut);
        await context.Entry(serverProgress).ReloadAsync();
        Assert.Equal(10, serverProgress.PagesRead);
        Assert.Equal("kobo.10.1", Assert.Single(context.AppUserKoboReadingLocation).LocationValue);

        var getState = Assert.IsType<JsonArray>(
            await koboService.GetReadingStateAsync(token, entitlementId));
        Assert.Equal("Finished", getState[0]!["StatusInfo"]!["Status"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReadingState_NoBackfill_ExistingProgressHasNoLocationUntilPut()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "No Backfill", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var entitlementId = KoboEntitlementId.FromChapterIdString(chapter.Id);

        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 5,
            BookScrollId = "//body/p[1]",
        });
        await context.SaveChangesAsync();

        Assert.Empty(context.AppUserKoboReadingLocation);

        var getState = Assert.IsType<JsonArray>(
            await koboService.GetReadingStateAsync(token, entitlementId));
        Assert.Equal(50, getState[0]!["CurrentBookmark"]!["ProgressPercent"]!.GetValue<double>());
        Assert.Null(getState[0]!["CurrentBookmark"]!["Location"]);
        Assert.Empty(context.AppUserKoboReadingLocation);
    }

    [Fact]
    public async Task SyncLibrary_NestsReadingState_AndEmitsChangedReadingState()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Sync Progress", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var entitlementId = KoboEntitlementId.FromChapterIdString(chapter.Id);

        // Pre-existing progress + Location → nested on NewEntitlement
        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 5,
        });
        context.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            LocationValue = "kobo.5.1",
            LocationType = "KoboSpan",
            LocationSource = "OEBPS/mid.xhtml",
        });
        await context.SaveChangesAsync();

        var first = await koboService.SyncLibraryAsync(token, syncTokenHeader: null);
        Assert.False(first.Continue);
        var entitlement = first.Items[0]!["NewEntitlement"]!.AsObject();
        Assert.True(entitlement.ContainsKey("ReadingState"));
        Assert.Equal("Reading",
            entitlement["ReadingState"]!["StatusInfo"]!["Status"]!.GetValue<string>());
        Assert.Equal(50,
            entitlement["ReadingState"]!["CurrentBookmark"]!["ProgressPercent"]!.GetValue<double>());
        Assert.Equal("kobo.5.1",
            entitlement["ReadingState"]!["CurrentBookmark"]!["Location"]!["Value"]!.GetValue<string>());

        var tokenAfterFirst = KoboSyncToken.FromHeader(first.SyncToken);
        Assert.True(tokenAfterFirst.ReadingStateLastModified > DateTime.MinValue);

        // Later progress change → ChangedReadingState on next sync (Location still present)
        var progress = Assert.Single(context.AppUserProgresses);
        progress.PagesRead = 10;
        await context.SaveChangesAsync();

        var second = await koboService.SyncLibraryAsync(token, first.SyncToken);
        Assert.False(second.Continue);
        var changed = Assert.Single(second.Items, i => i!["ChangedReadingState"] != null);
        var state = changed!["ChangedReadingState"]!["ReadingState"]!.AsObject();
        Assert.Equal(entitlementId, state["EntitlementId"]!.GetValue<string>());
        Assert.Equal("Finished", state["StatusInfo"]!["Status"]!.GetValue<string>());
        Assert.Equal(100, state["CurrentBookmark"]!["ProgressPercent"]!.GetValue<double>());
        Assert.Equal("kobo.5.1", state["CurrentBookmark"]!["Location"]!["Value"]!.GetValue<string>());

        var tokenAfterSecond = KoboSyncToken.FromHeader(second.SyncToken);
        Assert.True(tokenAfterSecond.ReadingStateLastModified >= tokenAfterFirst.ReadingStateLastModified);

        // Third sync with same token watermark: no further reading-state items
        var third = await koboService.SyncLibraryAsync(token, second.SyncToken);
        Assert.DoesNotContain(third.Items, i =>
            i!["ChangedReadingState"] != null
            || i!["NewEntitlement"]?["ReadingState"] != null
            || i!["ChangedEntitlement"]?["ReadingState"] != null);
    }

    [Fact]
    public async Task SyncLibrary_ReturnsNativeEpubEntitlements_WithStableUuidAndMetadata()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Dune", chapterNumber: "1", titleName: "Arrival",
            writerName: "Frank Herbert", language: "en", summary: "Chapter summary",
            releaseDate: new DateTime(1965, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", baseUrl: "/base/");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var result = await koboService.SyncLibraryAsync(token, syncTokenHeader: null);

        Assert.False(result.Continue);
        Assert.False(string.IsNullOrWhiteSpace(result.SyncToken));
        Assert.Single(result.Items);

        var wrapper = result.Items[0]!.AsObject();
        Assert.True(wrapper.ContainsKey("NewEntitlement"));
        var entitlement = wrapper["NewEntitlement"]!.AsObject();
        Assert.False(entitlement.ContainsKey("ReadingState"));

        var expectedUuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var bookEntitlement = entitlement["BookEntitlement"]!.AsObject();
        Assert.Equal(expectedUuid, bookEntitlement["Id"]!.GetValue<string>());
        Assert.Equal(expectedUuid, bookEntitlement["RevisionId"]!.GetValue<string>());
        Assert.Equal(expectedUuid, bookEntitlement["CrossRevisionId"]!.GetValue<string>());
        Assert.False(bookEntitlement["IsRemoved"]!.GetValue<bool>());

        var metadata = entitlement["BookMetadata"]!.AsObject();
        Assert.Equal(expectedUuid, metadata["EntitlementId"]!.GetValue<string>());
        Assert.Equal(expectedUuid, metadata["WorkId"]!.GetValue<string>());
        Assert.Equal(expectedUuid, metadata["CoverImageId"]!.GetValue<string>());
        Assert.Equal("Dune - Arrival", metadata["Title"]!.GetValue<string>());
        Assert.Equal("Chapter summary", metadata["Description"]!.GetValue<string>());
        Assert.Equal("en", metadata["Language"]!.GetValue<string>());
        Assert.Equal("Frank Herbert", metadata["Contributors"]![0]!.GetValue<string>());
        Assert.Equal("Dune", metadata["Series"]!["Name"]!.GetValue<string>());
        Assert.Equal(1f, metadata["Series"]!["Number"]!.GetValue<float>());
        Assert.Equal(1f, metadata["Series"]!["NumberFloat"]!.GetValue<float>());

        var downloadUrls = metadata["DownloadUrls"]!.AsArray();
        Assert.Equal(2, downloadUrls.Count);
        Assert.Equal(KoboService.Epub3Format, downloadUrls[0]!["Format"]!.GetValue<string>());
        Assert.Equal(KoboService.EpubFormat, downloadUrls[1]!["Format"]!.GetValue<string>());
        Assert.Equal(
            $"https://kavita.example.com/base/api/kobo/{token}/download/{expectedUuid}/epub",
            downloadUrls[0]!["Url"]!.GetValue<string>());
        Assert.Equal(downloadUrls[0]!["Url"]!.GetValue<string>(),
            downloadUrls[1]!["Url"]!.GetValue<string>());

        Assert.Equal(1, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));

        // Second sync is empty (durable cursor) but still round-trips synctoken
        var page2 = await koboService.SyncLibraryAsync(token, result.SyncToken);
        Assert.Empty(page2.Items);
        Assert.False(page2.Continue);
        Assert.False(string.IsNullOrWhiteSpace(page2.SyncToken));
        _ = library;
    }

    [Fact]
    public async Task SyncLibrary_PaginatesAtConfiguredPageSize_WithContinueHeaderFlag()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        const int pageSize = 3;
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", syncPageSize: pageSize);
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        for (var i = 0; i < pageSize + 2; i++)
        {
            await AddEpubChapter(context, library, $"Series {i}", "1");
        }

        await context.SaveChangesAsync();

        var page1 = await koboService.SyncLibraryAsync(token, null);
        Assert.Equal(pageSize, page1.Items.Count);
        Assert.True(page1.Continue);

        var page2 = await koboService.SyncLibraryAsync(token, page1.SyncToken);
        Assert.Equal(2, page2.Items.Count);
        Assert.False(page2.Continue);

        Assert.Equal(pageSize + 2,
            await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));
    }

    [Fact]
    public async Task SyncLibrary_PageSizeChange_AppliesOnNextContinueWithoutResettingWatermark()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", syncPageSize: 2);
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        for (var i = 0; i < 5; i++)
        {
            await AddEpubChapter(context, library, $"Series {i}", "1");
        }

        await context.SaveChangesAsync();

        var page1 = await koboService.SyncLibraryAsync(token, null);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.Continue);
        Assert.False(string.IsNullOrWhiteSpace(page1.SyncToken));

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", syncPageSize: 3);

        var page2 = await koboService.SyncLibraryAsync(token, page1.SyncToken);
        Assert.Equal(3, page2.Items.Count);
        Assert.False(page2.Continue);
        Assert.False(string.IsNullOrWhiteSpace(page2.SyncToken));

        Assert.Equal(5, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));
    }

    [Fact]
    public async Task SyncLibrary_Eligibility_RequiresAllowKoboSyncAndEpubOrCbz()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var allowed = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");

        var disallowed = new LibraryBuilder("No Kobo").WithAllowKoboSync(false).Build();
        unitOfWork.LibraryRepository.Add(disallowed);
        await unitOfWork.CommitAsync();

        var trackedUser = await context.AppUser.Include(u => u.Libraries).SingleAsync(u => u.Id == user.Id);
        trackedUser.Libraries.Add(disallowed);
        await context.SaveChangesAsync();

        var epubChapter = await AddEpubChapter(context, allowed, "Epub Book", "1");
        var cbzChapter = await AddCbzChapter(context, allowed, "Cbz Book", "1");
        await AddChapterWithFormat(context, allowed, "Pdf Book", "1", MangaFormat.Pdf);
        await AddChapterWithFormat(context, disallowed, "Disallowed Epub", "1", MangaFormat.Epub);
        await context.SaveChangesAsync();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var result = await koboService.SyncLibraryAsync(token, null);
        Assert.Equal(2, result.Items.Count);
        var uuids = result.Items
            .Select(i => i!["NewEntitlement"]!["BookEntitlement"]!["Id"]!.GetValue<string>())
            .ToHashSet();
        Assert.Contains(KoboEntitlementId.FromChapterIdString(epubChapter.Id), uuids);
        Assert.Contains(KoboEntitlementId.FromChapterIdString(cbzChapter.Id), uuids);

        // Convert-eligible chapters still advertise EPUB download URLs immediately.
        var cbzItem = result.Items.First(i =>
            i!["NewEntitlement"]!["BookEntitlement"]!["Id"]!.GetValue<string>() ==
            KoboEntitlementId.FromChapterIdString(cbzChapter.Id));
        var downloadUrls = cbzItem!["NewEntitlement"]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Equal(2, downloadUrls.Count);
        Assert.Equal(KoboService.EpubFormat, downloadUrls[1]!["Format"]!.GetValue<string>());
    }

    [Fact]
    public async Task PreferNativeEpub_WhenArchiveAlsoPresent_ForSyncDownloadAndMetadata()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");

        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Archive, 10).WithBytes(999).Build())
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(1234).Build())
            .Build();
        chapter.TitleName = "Mixed";
        var series = new SeriesBuilder("Mixed Formats")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var sync = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(sync.Items);
        var downloadUrls = sync.Items[0]!["NewEntitlement"]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Equal(2, downloadUrls.Count);
        Assert.Equal(KoboService.Epub3Format, downloadUrls[0]!["Format"]!.GetValue<string>());
        Assert.Equal(KoboService.EpubFormat, downloadUrls[1]!["Format"]!.GetValue<string>());
        Assert.Equal(1234, downloadUrls[0]!["Size"]!.GetValue<long>());
        Assert.Equal(1234, downloadUrls[1]!["Size"]!.GetValue<long>());

        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var download = await koboService.GetDownloadAsync(token, uuid, "epub");
        Assert.Equal(_epubPath, download.FilePath);
        Assert.Equal("application/epub+zip", download.ContentType);

        var downloadEpub3 = await koboService.GetDownloadAsync(token, uuid, "epub3");
        Assert.Equal(_epubPath, downloadEpub3.FilePath);

        var metadata = await koboService.GetMetadataAsync(token, uuid);
        Assert.Equal(KoboService.Epub3Format, metadata[0]["DownloadUrls"]![0]!["Format"]!.GetValue<string>());
        Assert.Equal(KoboService.EpubFormat, metadata[0]["DownloadUrls"]![1]!["Format"]!.GetValue<string>());
        Assert.StartsWith("https://kavita.example.com/api/kobo/",
            metadata[0]["DownloadUrls"]![0]!["Url"]!.GetValue<string>());
    }

    [Fact]
    public async Task KepubAdvertise_WhenEnabledAndCached_IsKepubOnly()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Kepub Series", "1");
        await context.SaveChangesAsync();

        const string kepubPath = "/tmp/kobo-test/AesopsFables.kepub.epub";
        var conversion = Substitute.For<IKoboConversionService>();
        conversion.TryGetCachedKepubPath(chapter.Id, Arg.Any<MangaFile>())
            .Returns(kepubPath);

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var sync = await koboService.SyncLibraryAsync(token, null);
        var downloadUrls = sync.Items[0]!["NewEntitlement"]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Single(downloadUrls);
        Assert.Equal(KoboService.KepubFormat, downloadUrls[0]!["Format"]!.GetValue<string>());
        Assert.Equal(
            $"https://kavita.example.com/api/kobo/{token}/download/{uuid}/kepub",
            downloadUrls[0]!["Url"]!.GetValue<string>());

        var metadata = await koboService.GetMetadataAsync(token, uuid);
        Assert.Single(metadata[0]["DownloadUrls"]!.AsArray());
        Assert.Equal(KoboService.KepubFormat, metadata[0]["DownloadUrls"]![0]!["Format"]!.GetValue<string>());
    }

    [Fact]
    public async Task KepubAdvertise_WhenEnabledWithoutCache_FallsBackToEpub()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Epub Fallback Series", "1");
        await context.SaveChangesAsync();

        var conversion = Substitute.For<IKoboConversionService>();
        conversion.TryGetCachedKepubPath(Arg.Any<int>(), Arg.Any<MangaFile>()).Returns((string?)null);

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var sync = await koboService.SyncLibraryAsync(token, null);
        var downloadUrls = sync.Items[0]!["NewEntitlement"]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Equal(2, downloadUrls.Count);
        Assert.Equal(KoboService.Epub3Format, downloadUrls[0]!["Format"]!.GetValue<string>());
        Assert.Equal(KoboService.EpubFormat, downloadUrls[1]!["Format"]!.GetValue<string>());
        Assert.EndsWith("/epub", downloadUrls[0]!["Url"]!.GetValue<string>());
    }

    [Fact]
    public async Task KepubAdvertise_WhenDisabled_IgnoresCachedKepub()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Kepub Disabled Series", "1");
        await context.SaveChangesAsync();

        var conversion = Substitute.For<IKoboConversionService>();
        conversion.TryGetCachedKepubPath(Arg.Any<int>(), Arg.Any<MangaFile>())
            .Returns("/tmp/should-not-advertise.kepub.epub");

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: false);
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var sync = await koboService.SyncLibraryAsync(token, null);
        var downloadUrls = sync.Items[0]!["NewEntitlement"]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Equal(2, downloadUrls.Count);
        Assert.Equal(KoboService.Epub3Format, downloadUrls[0]!["Format"]!.GetValue<string>());
        Assert.Equal(KoboService.EpubFormat, downloadUrls[1]!["Format"]!.GetValue<string>());
        conversion.DidNotReceive().TryGetCachedKepubPath(Arg.Any<int>(), Arg.Any<MangaFile>());
    }

    [Fact]
    public async Task Download_Kepub_UsesKepubEpubContentDisposition()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Kepub Download Series", "1");
        await context.SaveChangesAsync();

        var source = chapter.Files.Single(f => f.Format == MangaFormat.Epub);
        var kepubPath = Path.Combine(Path.GetTempPath(), $"kobo-kepub-{Guid.NewGuid():N}.kepub.epub");
        await File.WriteAllTextAsync(kepubPath, "kepub-bytes");
        try
        {
            var conversion = Substitute.For<IKoboConversionService>();
            conversion.GetOrConvertKepubAsync(chapter.Id, Arg.Any<MangaFile>(), Arg.Any<string>(),
                    Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(kepubPath);

            await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
                enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");
            var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
            var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
            var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

            var download = await koboService.GetDownloadAsync(token, uuid, "kepub");
            Assert.Equal(kepubPath, download.FilePath);
            Assert.Equal("application/epub+zip", download.ContentType);
            Assert.EndsWith(KoboService.KepubDownloadFileExtension, download.FileDownloadName);
            Assert.Equal(KoboService.BuildKepubDownloadFileName(source), download.FileDownloadName);
        }
        finally
        {
            if (File.Exists(kepubPath)) File.Delete(kepubPath);
        }
    }

    [Fact]
    public async Task Download_Kepub_Throws_WhenConversionDisabled()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Kepub Disabled Download", "1");
        await context.SaveChangesAsync();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: false);
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.GetDownloadAsync(token, uuid, "kepub"));
        Assert.Equal("kobo-format-unsupported", ex.Message);
    }

    [Fact]
    public async Task BuildBookMetadata_OmitsSeriesNumber_WhenChapterNumberIsDefault()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");

        var chapter = new ChapterBuilder(Parser.DefaultChapter)
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).Build())
            .Build();
        chapter.TitleName = "Oneshot";
        var series = new SeriesBuilder("No Number Series")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var sync = await koboService.SyncLibraryAsync(token, null);
        var seriesMeta = sync.Items[0]!["NewEntitlement"]!["BookMetadata"]!["Series"]!.AsObject();
        Assert.Equal("No Number Series", seriesMeta["Name"]!.GetValue<string>());
        Assert.False(seriesMeta.ContainsKey("Number"));
        Assert.False(seriesMeta.ContainsKey("NumberFloat"));
    }

    [Fact]
    public async Task GetCover_UsesChapterThenVolumeThenSeriesFallback()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var coverDir = Path.Join(Path.GetTempPath(), "kavita-kobo-covers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverDir);
        var chapterCover = "chapter-cover.jpg";
        var volumeCover = "volume-cover.jpg";
        var seriesCover = "series-cover.jpg";
        await File.WriteAllBytesAsync(Path.Join(coverDir, chapterCover), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Join(coverDir, volumeCover), [4, 5, 6]);
        await File.WriteAllBytesAsync(Path.Join(coverDir, seriesCover), [7, 8, 9]);

        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).Build())
            .Build();
        var volume = new VolumeBuilder(Parser.LooseLeafVolume)
            .WithChapter(chapter)
            .Build();
        var series = new SeriesBuilder("Cover Series")
            .WithFormat(MangaFormat.Epub)
            .WithCoverImage(seriesCover)
            .WithVolume(volume)
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper, coverDir);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        // Series cover when chapter/volume empty
        var seriesOnly = await koboService.GetCoverAsync(token, uuid);
        Assert.NotNull(seriesOnly);
        Assert.EndsWith(seriesCover, seriesOnly!.FilePath);

        // Volume wins over series
        volume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        volume.CoverImage = volumeCover;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var volumeHit = await koboService.GetCoverAsync(token, uuid);
        Assert.EndsWith(volumeCover, volumeHit!.FilePath);

        // Chapter wins over volume
        chapter = await context.Chapter.SingleAsync(c => c.Id == chapter.Id);
        chapter.CoverImage = chapterCover;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var chapterHit = await koboService.GetCoverAsync(token, uuid);
        Assert.EndsWith(chapterCover, chapterHit!.FilePath);
    }

    [Fact]
    public void EntitlementId_IsStableUuidV5()
    {
        var a = KoboEntitlementId.FromChapterId(42);
        var b = KoboEntitlementId.FromChapterId(42);
        var c = KoboEntitlementId.FromChapterId(43);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(5, a.Version);
    }

    [Fact]
    public void TagId_IsStableUuidV5_DistinctNamespaces()
    {
        var listA = KoboTagId.FromReadingListId(7);
        var listB = KoboTagId.FromReadingListId(7);
        var collection = KoboTagId.FromCollectionId(7);
        Assert.Equal(listA, listB);
        Assert.NotEqual(listA, collection);
        Assert.Equal(5, listA.Version);
        Assert.Equal(5, collection.Version);
    }

    [Fact]
    public async Task SyncLibrary_EmitsNewTag_ForReadingListAndCollection_WithEligibleItems()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, epubChapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Shelf Series", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var pdfChapter = await AddChapterWithFormat(context, library, "Pdf Series", "1", MangaFormat.Pdf);

        var readingList = new ReadingListBuilder("My List")
            .WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, epubChapter.Volume.SeriesId, epubChapter.VolumeId, epubChapter.Id).Build())
            .WithItem(new ReadingListItemBuilder(1, pdfChapter.Volume.SeriesId, pdfChapter.VolumeId, pdfChapter.Id).Build())
            .Build();
        user = await context.AppUser.Include(u => u.ReadingLists).SingleAsync(u => u.Id == user.Id);
        user.ReadingLists.Add(readingList);

        var series = await context.Series.SingleAsync(s => s.Name == "Shelf Series");
        var pdfSeries = await context.Series.SingleAsync(s => s.Name == "Pdf Series");
        user.Collections = [new AppUserCollectionBuilder("My Collection").WithItems([series, pdfSeries]).Build()];
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        readingList = await context.ReadingList.SingleAsync(r => r.Title == "My List");
        var collection = await context.AppUserCollection.SingleAsync(c => c.Title == "My Collection");
        var expectedListUuid = KoboTagId.FromReadingListIdString(readingList.Id);
        var expectedCollectionUuid = KoboTagId.FromCollectionIdString(collection.Id);
        var epubUuid = KoboEntitlementId.FromChapterIdString(epubChapter.Id);
        var pdfUuid = KoboEntitlementId.FromChapterIdString(pdfChapter.Id);

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var sync = await koboService.SyncLibraryAsync(token, null);
        var newTags = sync.Items.Where(i => i!["NewTag"] != null).ToList();
        Assert.Equal(2, newTags.Count);

        var listTag = newTags.Select(i => i!["NewTag"]!["Tag"]!.AsObject())
            .Single(t => t["Id"]!.GetValue<string>() == expectedListUuid);
        Assert.Equal("My List", listTag["Name"]!.GetValue<string>());
        Assert.Equal("UserTag", listTag["Type"]!.GetValue<string>());
        var listItems = listTag["Items"]!.AsArray().Select(i => i!["RevisionId"]!.GetValue<string>()).ToList();
        Assert.Contains(epubUuid, listItems);
        Assert.DoesNotContain(pdfUuid, listItems);

        var collectionTag = newTags.Select(i => i!["NewTag"]!["Tag"]!.AsObject())
            .Single(t => t["Id"]!.GetValue<string>() == expectedCollectionUuid);
        Assert.Equal("My Collection", collectionTag["Name"]!.GetValue<string>());
        var collectionItems = collectionTag["Items"]!.AsArray().Select(i => i!["RevisionId"]!.GetValue<string>()).ToList();
        Assert.Contains(epubUuid, collectionItems);
        Assert.DoesNotContain(pdfUuid, collectionItems);

        var syncToken = KoboSyncToken.FromHeader(sync.SyncToken);
        Assert.True(syncToken.TagsLastModified > DateTime.MinValue);

        // Incremental: no tag re-emit when watermark is current
        var second = await koboService.SyncLibraryAsync(token, sync.SyncToken);
        Assert.DoesNotContain(second.Items, i => i!["NewTag"] != null || i!["ChangedTag"] != null);
    }

    [Fact]
    public async Task SyncLibrary_RenameEmitsChangedTag_SameUuid()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Rename Series", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        user = await context.AppUser.Include(u => u.ReadingLists).SingleAsync(u => u.Id == user.Id);
        user.ReadingLists.Add(new ReadingListBuilder("Before")
            .WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, chapter.Volume.SeriesId, chapter.VolumeId, chapter.Id).Build())
            .Build());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var first = await koboService.SyncLibraryAsync(token, null);
        var list = await context.ReadingList.SingleAsync(r => r.Title == "Before");
        var tagUuid = KoboTagId.FromReadingListIdString(list.Id);
        Assert.Contains(first.Items, i => i!["NewTag"]?["Tag"]?["Id"]?.GetValue<string>() == tagUuid);

        list = await context.ReadingList.SingleAsync(r => r.Id == list.Id);
        list.Title = "After";
        list.NormalizedTitle = "after";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var second = await koboService.SyncLibraryAsync(token, first.SyncToken);
        var changed = Assert.Single(second.Items, i => i!["ChangedTag"] != null);
        var tag = changed!["ChangedTag"]!["Tag"]!.AsObject();
        Assert.Equal(tagUuid, tag["Id"]!.GetValue<string>());
        Assert.Equal("After", tag["Name"]!.GetValue<string>());
    }

    [Fact]
    public async Task SyncLibrary_DeleteEmitsDeletedTag_ThenClearsTombstone()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Delete List Series", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        user = await context.AppUser.Include(u => u.ReadingLists).SingleAsync(u => u.Id == user.Id);
        user.ReadingLists.Add(new ReadingListBuilder("Doomed")
            .WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, chapter.Volume.SeriesId, chapter.VolumeId, chapter.Id).Build())
            .Build());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var first = await koboService.SyncLibraryAsync(token, null);
        var list = await context.ReadingList.SingleAsync(r => r.Title == "Doomed");
        var tagUuid = KoboTagId.FromReadingListId(list.Id);

        user = await context.AppUser.Include(u => u.ReadingLists).SingleAsync(u => u.Id == user.Id);
        var readingListService = new ReadingListService(unitOfWork,
            Substitute.For<ILogger<ReadingListService>>(), Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(), Substitute.For<IDirectoryService>(),
            new EntityNamingService(), koboService);
        Assert.True(await readingListService.DeleteReadingList(list.Id, user));
        Assert.Equal(1, await context.AppUserKoboTagTombstone.CountAsync(t => t.TagId == tagUuid));

        var second = await koboService.SyncLibraryAsync(token, first.SyncToken);
        var deleted = Assert.Single(second.Items, i => i!["DeletedTag"] != null);
        Assert.Equal(tagUuid.ToString(), deleted!["DeletedTag"]!["Tag"]!["Id"]!.GetValue<string>());
        Assert.Equal(0, await context.AppUserKoboTagTombstone.CountAsync(t => t.TagId == tagUuid));

        var third = await koboService.SyncLibraryAsync(token, second.SyncToken);
        Assert.DoesNotContain(third.Items, i => i!["DeletedTag"] != null);
    }

    [Fact]
    public async Task SyncLibrary_TagsNotLimitedByPageSize_AndForceFullSyncResetsTagsWatermark()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, _) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Page Series 1", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddEpubChapter(context, library, "Page Series 2", "1");
        await AddEpubChapter(context, library, "Page Series 3", "1");

        user = await context.AppUser.Include(u => u.ReadingLists).SingleAsync(u => u.Id == user.Id);
        user.ReadingLists.Add(new ReadingListBuilder("Shelf A").WithAppUserId(user.Id).Build());
        user.ReadingLists.Add(new ReadingListBuilder("Shelf B").WithAppUserId(user.Id).Build());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", syncPageSize: 1);
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var page1 = await koboService.SyncLibraryAsync(token, null);
        Assert.True(page1.Continue);
        // One book slot + both tags (tags not page-limited)
        Assert.Equal(1, page1.Items.Count(i => i!["NewEntitlement"] != null || i!["ChangedEntitlement"] != null));
        Assert.Equal(2, page1.Items.Count(i => i!["NewTag"] != null));

        var afterFirst = KoboSyncToken.FromHeader(page1.SyncToken);
        Assert.True(afterFirst.TagsLastModified > DateTime.MinValue);

        // Finish remaining books without re-emitting tags
        var page2 = await koboService.SyncLibraryAsync(token, page1.SyncToken);
        Assert.DoesNotContain(page2.Items, i => i!["NewTag"] != null || i!["ChangedTag"] != null);
        var page3 = await koboService.SyncLibraryAsync(token, page2.SyncToken);
        Assert.False(page3.Continue);

        await koboService.ForceFullSyncAsync(user.Id);
        var afterForce = await koboService.SyncLibraryAsync(token, page3.SyncToken);
        Assert.Equal(2, afterForce.Items.Count(i => i!["NewTag"] != null));
        var forceToken = KoboSyncToken.FromHeader(afterForce.SyncToken);
        Assert.True(forceToken.TagsLastModified > DateTime.MinValue);
    }

    [Fact]
    public async Task DeviceTag_CreateRenameItemsDelete_MutatesOwnedReadingList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Device Tag Series", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var chapter2 = await AddEpubChapter(context, library, "Device Tag Series 2", "1");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var chapterUuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var chapter2Uuid = KoboEntitlementId.FromChapterIdString(chapter2.Id);

        var createBody = new JsonObject
        {
            ["Name"] = "From Device",
            ["Items"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "ProductRevisionTagItem",
                    ["RevisionId"] = chapterUuid,
                },
            },
        };
        var tagUuid = await koboService.CreateTagAsync(token, createBody);
        var list = await context.ReadingList.Include(r => r.Items).SingleAsync(r => r.Title == "From Device");
        Assert.Equal(user.Id, list.AppUserId);
        Assert.Equal(KoboTagId.FromReadingListIdString(list.Id), tagUuid);
        Assert.Single(list.Items);
        Assert.Equal(chapter.Id, list.Items.Single().ChapterId);

        // Subsequent sync uses readinglist:{id} identity
        var sync = await koboService.SyncLibraryAsync(token, null);
        Assert.Contains(sync.Items, i =>
            i!["NewTag"]?["Tag"]?["Id"]?.GetValue<string>() == tagUuid
            && i!["NewTag"]!["Tag"]!["Name"]!.GetValue<string>() == "From Device");

        await koboService.RenameTagAsync(token, tagUuid, new JsonObject { ["Name"] = "Renamed Shelf" });
        list = await context.ReadingList.SingleAsync(r => r.Id == list.Id);
        Assert.Equal("Renamed Shelf", list.Title);

        await koboService.AddTagItemsAsync(token, tagUuid, new JsonObject
        {
            ["Items"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "ProductRevisionTagItem",
                    ["RevisionId"] = chapter2Uuid,
                },
            },
        });
        list = await context.ReadingList.Include(r => r.Items).SingleAsync(r => r.Id == list.Id);
        Assert.Equal(2, list.Items.Count);

        await koboService.RemoveTagItemsAsync(token, tagUuid, new JsonObject
        {
            ["Items"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "ProductRevisionTagItem",
                    ["RevisionId"] = chapterUuid,
                },
            },
        });
        list = await context.ReadingList.Include(r => r.Items).SingleAsync(r => r.Id == list.Id);
        Assert.Single(list.Items);
        Assert.Equal(chapter2.Id, list.Items.Single().ChapterId);

        // Same-name create merges items
        var mergeUuid = await koboService.CreateTagAsync(token, new JsonObject
        {
            ["Name"] = "Renamed Shelf",
            ["Items"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "ProductRevisionTagItem",
                    ["RevisionId"] = chapterUuid,
                },
            },
        });
        Assert.Equal(tagUuid, mergeUuid);
        list = await context.ReadingList.Include(r => r.Items).SingleAsync(r => r.Id == list.Id);
        Assert.Equal(2, list.Items.Count);

        await koboService.DeleteTagAsync(token, tagUuid);
        Assert.False(await context.ReadingList.AnyAsync(r => r.Id == list.Id));
        Assert.Equal(1, await context.AppUserKoboTagTombstone.CountAsync(t =>
            t.TagId == Guid.Parse(tagUuid) && t.AppUserId == user.Id));
    }

    [Fact]
    public async Task DeviceTag_RejectsCollectionAndNonOwnedList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Reject Series", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var other = new AppUserBuilder("otherkobo", "other@localhost")
            .WithLibrary(library)
            .WithLocale("en")
            .WithRole(PolicyConstants.LoginRole)
            .Build();
        context.AppUser.Add(other);
        await context.SaveChangesAsync();

        other = await context.AppUser.Include(u => u.ReadingLists).Include(u => u.Collections)
            .SingleAsync(u => u.UserName == "otherkobo");
        other.ReadingLists.Add(new ReadingListBuilder("Promoted List")
            .WithAppUserId(other.Id)
            .WithPromoted(true)
            .WithItem(new ReadingListItemBuilder(0, chapter.Volume.SeriesId, chapter.VolumeId, chapter.Id)
                .Build())
            .Build());
        await context.SaveChangesAsync();

        // Collection owned by the acting user — visible on device, but mutations rejected.
        user = await context.AppUser.Include(u => u.Collections).SingleAsync(u => u.Id == user.Id);
        var series = await context.Series.SingleAsync(s => s.Name == "Reject Series");
        user.Collections =
        [
            new AppUserCollectionBuilder("Owned Collection").WithItems([series]).Build(),
        ];
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var promotedList = await context.ReadingList.SingleAsync(r => r.Title == "Promoted List");
        var collection = await context.AppUserCollection.SingleAsync(c => c.Title == "Owned Collection");
        var promotedUuid = KoboTagId.FromReadingListIdString(promotedList.Id);
        var collectionUuid = KoboTagId.FromCollectionIdString(collection.Id);

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var renameBody = new JsonObject { ["Name"] = "Hacked" };
        var itemsBody = new JsonObject
        {
            ["Items"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "ProductRevisionTagItem",
                    ["RevisionId"] = KoboEntitlementId.FromChapterIdString(chapter.Id),
                },
            },
        };

        var promotedRename = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.RenameTagAsync(token, promotedUuid, renameBody));
        Assert.Equal(KoboService.TagForbiddenMessage, promotedRename.Message);

        var collectionRename = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.RenameTagAsync(token, collectionUuid, renameBody));
        Assert.Equal(KoboService.TagForbiddenMessage, collectionRename.Message);

        var collectionAdd = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.AddTagItemsAsync(token, collectionUuid, itemsBody));
        Assert.Equal(KoboService.TagForbiddenMessage, collectionAdd.Message);

        var collectionDelete = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.DeleteTagAsync(token, collectionUuid));
        Assert.Equal(KoboService.TagForbiddenMessage, collectionDelete.Message);

        var unknown = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.DeleteTagAsync(token, Guid.NewGuid().ToString()));
        Assert.Equal(KoboService.TagNotFoundMessage, unknown.Message);

        // Nothing mutated
        Assert.Equal("Promoted List",
            (await context.ReadingList.SingleAsync(r => r.Id == promotedList.Id)).Title);
        Assert.Equal("Owned Collection",
            (await context.AppUserCollection.SingleAsync(c => c.Id == collection.Id)).Title);
        Assert.Equal(0, await context.AppUserKoboTagTombstone.CountAsync());
    }

    [Fact]
    public async Task DeviceTag_Create_RequiresName()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.CreateTagAsync(token, new JsonObject { ["Name"] = "  ", ["Items"] = new JsonArray() }));
        Assert.Equal(KoboService.TagNameRequiredMessage, ex.Message);
        Assert.Equal(0, await context.ReadingList.CountAsync());
    }

    [Fact]
    public async Task DeleteEntitlement_ArchivesAndNextSyncEmitsIsRemoved()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Delete Me", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var first = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(first.Items);
        Assert.False(first.Items[0]!["NewEntitlement"]!["BookEntitlement"]!["IsRemoved"]!.GetValue<bool>());

        await koboService.DeleteEntitlementAsync(token, uuid);

        Assert.True(await context.Chapter.AnyAsync(c => c.Id == chapter.Id));
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.ChapterId == chapter.Id));
        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s =>
            s.AppUserId == user.Id && s.ChapterId == chapter.Id));

        var removal = await koboService.SyncLibraryAsync(token, first.SyncToken);
        Assert.Single(removal.Items);
        var book = removal.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                   ?? removal.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.True(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());
        Assert.Equal(1, await context.AppUserKoboSyncedChapter.CountAsync(s =>
            s.AppUserId == user.Id && s.ChapterId == chapter.Id));

        var again = await koboService.SyncLibraryAsync(token, removal.SyncToken);
        Assert.Empty(again.Items);
    }

    [Fact]
    public async Task EligibilityLoss_ArchivesAndNextSyncEmitsIsRemoved()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Lose Access", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var first = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(first.Items);

        library = await context.Library.SingleAsync(l => l.Id == library.Id);
        library.AllowKoboSync = false;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var removal = await koboService.SyncLibraryAsync(token, first.SyncToken);
        Assert.Single(removal.Items);
        var book = removal.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                   ?? removal.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.True(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.ChapterId == chapter.Id));

        var again = await koboService.SyncLibraryAsync(token, removal.SyncToken);
        Assert.Empty(again.Items);
    }

    [Fact]
    public async Task HardDelete_CreatesTombstone_AndSyncEmitsIsRemovedOnce()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Hard Delete", chapterNumber: "1", titleName: "Gone",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var chapterId = chapter.Id;

        var first = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(first.Items);

        await koboService.PrepareHardDeleteAsync([chapterId]);
        Assert.Equal(1, await context.AppUserKoboTombstone.CountAsync(t =>
            t.AppUserId == user.Id && t.ChapterId == chapterId));
        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s =>
            s.AppUserId == user.Id && s.ChapterId == chapterId));

        var series = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters)
            .SingleAsync(s => s.Name == "Hard Delete");
        context.Series.Remove(series);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.False(await context.Chapter.AnyAsync(c => c.Id == chapterId));
        Assert.Equal(1, await context.AppUserKoboTombstone.CountAsync(t => t.ChapterId == chapterId));

        var removal = await koboService.SyncLibraryAsync(token, first.SyncToken);
        Assert.Single(removal.Items);
        var book = removal.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.True(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());
        Assert.Equal(0, await context.AppUserKoboTombstone.CountAsync(t => t.ChapterId == chapterId));

        var again = await koboService.SyncLibraryAsync(token, removal.SyncToken);
        Assert.Empty(again.Items);
    }

    [Fact]
    public async Task ForceFullSync_ClearsSyncedSetOnly_LeavesArchivesAndAuthKey()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Force Sync", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var syncUrl = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        var token = syncUrl.Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        await koboService.SyncLibraryAsync(token, null);
        await koboService.DeleteEntitlementAsync(token, uuid);
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a => a.AppUserId == user.Id));
        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));

        // Deliver IsRemoved so synced-set has a row again while archive remains
        await koboService.SyncLibraryAsync(token, null);
        Assert.Equal(1, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a => a.AppUserId == user.Id));

        await koboService.ForceFullSyncAsync(user.Id);

        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a => a.AppUserId == user.Id));
        var afterUrl = await koboService.GetOrCreateSyncUrlAsync(user.Id);
        Assert.Equal(syncUrl, afterUrl);

        // Cursor reset: next sync re-emits IsRemoved for the still-archived chapter (not the eligible add)
        var afterForce = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(afterForce.Items);
        var book = afterForce.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                   ?? afterForce.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.True(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());
    }

    [Fact]
    public async Task EligibilityRestore_AutoClearsArchive_AndReEntitlesWithSameUuid()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Restore Eligibility", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var first = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(first.Items);

        library = await context.Library.SingleAsync(l => l.Id == library.Id);
        library.AllowKoboSync = false;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var removal = await koboService.SyncLibraryAsync(token, first.SyncToken);
        Assert.Single(removal.Items);
        Assert.True((removal.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                     ?? removal.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!)["IsRemoved"]!
            .GetValue<bool>());
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.ChapterId == chapter.Id && !a.IsDeviceDeleted));

        library = await context.Library.SingleAsync(l => l.Id == library.Id);
        library.AllowKoboSync = true;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var restored = await koboService.SyncLibraryAsync(token, removal.SyncToken);
        Assert.Single(restored.Items);
        var book = restored.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                   ?? restored.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.False(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());
        Assert.Equal(0, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.ChapterId == chapter.Id));
    }

    [Fact]
    public async Task RestoreRemovedBooks_ClearsDeviceArchivesAndSynced_ReEntitlesSameUuid()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Device Restore", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        await koboService.SyncLibraryAsync(token, null);
        await koboService.DeleteEntitlementAsync(token, uuid);
        var removal = await koboService.SyncLibraryAsync(token, null);
        Assert.True((removal.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                     ?? removal.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!)["IsRemoved"]!
            .GetValue<bool>());
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.IsDeviceDeleted));
        Assert.Equal(1, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));

        // Force full sync must still leave the device archive (regression vs ticket 04)
        await koboService.ForceFullSyncAsync(user.Id);
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.IsDeviceDeleted));

        await koboService.RestoreRemovedBooksAsync(user.Id);

        Assert.Equal(0, await context.AppUserKoboArchivedChapter.CountAsync(a => a.AppUserId == user.Id));
        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));

        var again = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(again.Items);
        var book = again.Items[0]!["NewEntitlement"]?["BookEntitlement"]
                   ?? again.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.False(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());

        // Keep library reference used (eligible throughout)
        Assert.True(library.AllowKoboSync);
    }

    [Fact]
    public async Task RestoreRemovedBooks_DoesNotRestoreTombstonedHardDeletes()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Tombstone Restore", chapterNumber: "1", titleName: "Gone",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var chapterId = chapter.Id;

        await koboService.SyncLibraryAsync(token, null);
        await koboService.PrepareHardDeleteAsync([chapterId]);
        Assert.Equal(1, await context.AppUserKoboTombstone.CountAsync(t =>
            t.AppUserId == user.Id && t.ChapterId == chapterId));

        var series = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters)
            .SingleAsync(s => s.Name == "Tombstone Restore");
        context.Series.Remove(series);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await koboService.RestoreRemovedBooksAsync(user.Id);

        Assert.Equal(1, await context.AppUserKoboTombstone.CountAsync(t => t.ChapterId == chapterId));
        Assert.False(await context.Chapter.AnyAsync(c => c.Id == chapterId));

        var removal = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(removal.Items);
        var book = removal.Items[0]!["ChangedEntitlement"]!["BookEntitlement"]!;
        Assert.True(book["IsRemoved"]!.GetValue<bool>());
        Assert.Equal(uuid, book["Id"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetRemovedBooks_ListsOnlyEligibleDeviceDeletes()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, eligible) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Restorable", chapterNumber: "1", titleName: "Keep Me",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var ineligible = await AddChapterWithFormat(context, library, "Pdf Gone", "1", MangaFormat.Pdf);
        await context.SaveChangesAsync();
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var eligibleUuid = KoboEntitlementId.FromChapterIdString(eligible.Id);

        await koboService.SyncLibraryAsync(token, null);
        await koboService.DeleteEntitlementAsync(token, eligibleUuid);
        await koboService.SyncLibraryAsync(token, null);

        // Ineligible archive (eligibility-loss style) must not appear.
        context.AppUserKoboArchivedChapter.Add(new AppUserKoboArchivedChapter
        {
            AppUserId = user.Id,
            ChapterId = ineligible.Id,
            IsDeviceDeleted = true,
            LastModifiedUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        // Hard-deleted chapter with tombstone must not appear.
        var hardDeleted = await AddEpubChapter(context, library, "Hard Delete", "1");
        await context.SaveChangesAsync();
        await koboService.SyncLibraryAsync(token, null);
        await koboService.PrepareHardDeleteAsync([hardDeleted.Id]);
        var hardSeries = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters)
            .SingleAsync(s => s.Name == "Hard Delete");
        context.Series.Remove(hardSeries);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var removed = await koboService.GetRemovedBooksAsync(user.Id);

        Assert.Single(removed);
        Assert.Equal(eligible.Id, removed[0].ChapterId);
        Assert.Equal("Restorable", removed[0].SeriesName);
        Assert.Contains("Keep Me", removed[0].Title);
        Assert.Equal(library.Id, removed[0].LibraryId);
    }

    [Fact]
    public async Task RestoreRemovedBooks_Selected_OnlyClearsChosenChapters()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, library, first) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Select A", chapterNumber: "1", titleName: "A",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var second = await AddEpubChapter(context, library, "Select B", "1");
        await context.SaveChangesAsync();
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var firstUuid = KoboEntitlementId.FromChapterIdString(first.Id);
        var secondUuid = KoboEntitlementId.FromChapterIdString(second.Id);

        // Sync until both chapters are in the synced-set (default page size covers both).
        string? syncToken = null;
        for (var i = 0; i < 5; i++)
        {
            var page = await koboService.SyncLibraryAsync(token, syncToken);
            syncToken = page.SyncToken;
            if (!page.Continue) break;
        }
        Assert.Equal(2, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));

        await koboService.DeleteEntitlementAsync(token, firstUuid);
        await koboService.DeleteEntitlementAsync(token, secondUuid);
        await koboService.SyncLibraryAsync(token, null);

        Assert.Equal(2, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.IsDeviceDeleted));

        await koboService.RestoreRemovedBooksAsync(user.Id, [first.Id]);

        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.IsDeviceDeleted && a.ChapterId == second.Id));
        Assert.False(await context.AppUserKoboArchivedChapter.AnyAsync(a =>
            a.AppUserId == user.Id && a.ChapterId == first.Id));
        Assert.False(await context.AppUserKoboSyncedChapter.AnyAsync(s =>
            s.AppUserId == user.Id && s.ChapterId == first.Id));

        var listed = await koboService.GetRemovedBooksAsync(user.Id);
        Assert.Single(listed);
        Assert.Equal(second.Id, listed[0].ChapterId);
    }

    [Fact]
    public async Task EligibilityRestore_DoesNotAutoClearDeviceDeletedArchives()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Keep Device Delete", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        await koboService.SyncLibraryAsync(token, null);
        await koboService.DeleteEntitlementAsync(token, uuid);
        await koboService.SyncLibraryAsync(token, null);

        var idle = await koboService.SyncLibraryAsync(token, null);
        Assert.Empty(idle.Items);
        Assert.Equal(1, await context.AppUserKoboArchivedChapter.CountAsync(a =>
            a.AppUserId == user.Id && a.IsDeviceDeleted));
    }

    [Fact]
    public async Task SyncLibrary_WaitsForPerUserLock_ThenSucceeds()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, _) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Lock Wait", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        KoboService.ResetSyncLockForTests();
        KoboService.SyncLockWaitOverride = TimeSpan.FromSeconds(5);
        try
        {
            var hold = await KoboService.HoldLibrarySyncLockForTestsAsync(user.Id);
            var syncTask = koboService.SyncLibraryAsync(token, null);

            await Task.Delay(100);
            Assert.False(syncTask.IsCompleted);

            hold.Dispose();
            var result = await syncTask;
            Assert.Single(result.Items);
        }
        finally
        {
            KoboService.ResetSyncLockForTests();
        }
    }

    [Fact]
    public async Task SyncLibrary_LockWaitTimeout_ThrowsSyncBusy()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        KoboService.ResetSyncLockForTests();
        KoboService.SyncLockWaitOverride = TimeSpan.FromMilliseconds(50);
        try
        {
            using var hold = await KoboService.HoldLibrarySyncLockForTestsAsync(user.Id);
            var ex = await Assert.ThrowsAsync<KavitaException>(() =>
                koboService.SyncLibraryAsync(token, null));
            Assert.Equal(KoboService.SyncBusyMessage, ex.Message);
        }
        finally
        {
            KoboService.ResetSyncLockForTests();
        }
    }

    [Fact]
    public async Task NonSyncRoutes_AreNotBlockedByLibrarySyncLock()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, _, chapter) = await SeedEpubChapter(unitOfWork, context,
            seriesName: "Unlocked Routes", chapterNumber: "1", titleName: "Ch1",
            writerName: "Author", language: "en", summary: "s",
            releaseDate: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        KoboService.ResetSyncLockForTests();
        try
        {
            using var hold = await KoboService.HoldLibrarySyncLockForTestsAsync(user.Id);

            Assert.Equal(user.Id, await koboService.ResolveUserIdAsync(token));
            var init = await koboService.GetInitializationAsync(token);
            Assert.NotNull(init.Resources["library_sync"]);
            var metadata = await koboService.GetMetadataAsync(token, uuid);
            Assert.Single(metadata);
            var download = await koboService.GetDownloadAsync(token, uuid, "epub");
            Assert.True(File.Exists(download.FilePath));
            Assert.NotNull(await koboService.GetReadingStateAsync(token, uuid));
            var putBody = new JsonObject
            {
                ["ReadingStates"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["StatusInfo"] = new JsonObject { ["Status"] = "ReadyToRead" },
                    },
                },
            };
            Assert.NotNull(await koboService.PutReadingStateAsync(token, uuid, putBody));
        }
        finally
        {
            KoboService.ResetSyncLockForTests();
        }
    }

    private async Task<(AppUser User, Library Library, Chapter Chapter)> SeedEpubChapter(
        IUnitOfWork unitOfWork, DataContext context, string seriesName, string chapterNumber,
        string titleName, string writerName, string language, string summary, DateTime releaseDate)
    {
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var writer = new PersonBuilder(writerName).Build();
        var chapter = new ChapterBuilder(chapterNumber)
            .WithPages(10)
            .WithReleaseDate(releaseDate)
            .WithPerson(writer, PersonRole.Writer)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(2048).Build())
            .Build();
        chapter.TitleName = titleName;
        chapter.Summary = summary;

        var series = new SeriesBuilder(seriesName)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Metadata.Language = language;
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        return (user, library, chapter);
    }

    private async Task<Chapter> AddEpubChapter(DataContext context, Library library, string seriesName,
        string chapterNumber)
    {
        return await AddChapterWithFormat(context, library, seriesName, chapterNumber, MangaFormat.Epub);
    }

    private async Task<Chapter> AddCbzChapter(DataContext context, Library library, string seriesName,
        string chapterNumber)
    {
        var chapter = new ChapterBuilder(chapterNumber)
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build())
            .Build();
        var series = new SeriesBuilder(seriesName)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        return chapter;
    }

    private async Task<Chapter> AddChapterWithFormat(DataContext context, Library library, string seriesName,
        string chapterNumber, MangaFormat format)
    {
        var chapter = new ChapterBuilder(chapterNumber)
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, format, 10).Build())
            .Build();
        var series = new SeriesBuilder(seriesName)
            .WithFormat(format)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        return chapter;
    }

    [Fact]
    public async Task Download_ConvertedArchive_CacheHitAndMiss_ThroughService()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddCbzChapter(context, library, "Convert Me", "1");

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", convertBudget: 30);
        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var convertCalls = 0;
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                convertCalls++;
                var output = ci.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "epub-bytes");
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(true);

        var conversion = new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            converter,
            Substitute.For<IKepubifyRunner>(),
            Substitute.For<IKoboConversionJobScheduler>(),
            unitOfWork,
            Substitute.For<IEventHub>());
        KoboConversionService.ResetInFlightForTests();

        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var first = await koboService.GetDownloadAsync(token, uuid, "epub");
        Assert.True(File.Exists(first.FilePath));
        Assert.Equal(1, convertCalls);

        var second = await koboService.GetDownloadAsync(token, uuid, "epub");
        Assert.Equal(first.FilePath, second.FilePath);
        Assert.Equal(1, convertCalls); // cache hit — no reconvert

        // Fingerprint change invalidates cache
        var file = await context.MangaFile.SingleAsync(f => f.ChapterId == chapter.Id);
        file.Bytes = 99999;
        file.LastModifiedUtc = DateTime.UtcNow.AddMinutes(1);
        await context.SaveChangesAsync();

        var third = await koboService.GetDownloadAsync(token, uuid, "epub");
        Assert.Equal(2, convertCalls);
        Assert.NotEqual(first.FilePath, third.FilePath);
    }

    [Fact]
    public async Task Download_ConvertUnavailable_PropagatesFromConversionService()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddCbzChapter(context, library, "Slow Convert", "1");

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", convertBudget: 1);
        var conversion = Substitute.For<IKoboConversionService>();
        conversion.GetOrConvertEpubAsync(chapter.Id, Arg.Any<MangaFile>(), Arg.Any<string>(), 1,
                Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new KavitaException(KoboConversionService.ConvertUnavailableMessage));

        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.GetDownloadAsync(token, uuid, "epub"));
        Assert.Equal(KoboConversionService.ConvertUnavailableMessage, ex.Message);
    }

    [Fact]
    public async Task Download_ConvertInFlight_ThrowsUnavailable_WithoutSecondJob()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddCbzChapter(context, library, "In Flight", "1");

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                started.TrySetResult();
                await release.Task;
                var output = ci.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "epub-bytes");
            });

        var scheduler = Substitute.For<IKoboConversionJobScheduler>();
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(true);

        var conversion = new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            converter,
            Substitute.For<IKepubifyRunner>(),
            scheduler,
            unitOfWork,
            Substitute.For<IEventHub>());
        KoboConversionService.ResetInFlightForTests();

        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var firstTask = koboService.GetDownloadAsync(token, uuid, "epub");
        await started.Task;

        var inflightEx = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.GetDownloadAsync(token, uuid, "epub"));
        Assert.Equal(KoboConversionService.ConvertUnavailableMessage, inflightEx.Message);
        scheduler.DidNotReceive().EnqueueBackgroundConvert(Arg.Any<int>());

        release.TrySetResult();
        var result = await firstTask;
        Assert.True(File.Exists(result.FilePath));
    }

    [Fact]
    public async Task Download_ConvertHardFail_ThrowsFailed()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddCbzChapter(context, library, "Broken Archive", "1");

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var conversion = Substitute.For<IKoboConversionService>();
        conversion.GetOrConvertEpubAsync(Arg.Any<int>(), Arg.Any<MangaFile>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new KavitaException(KoboConversionService.ConvertFailedMessage));

        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.GetDownloadAsync(token, uuid, "epub"));
        Assert.Equal(KoboConversionService.ConvertFailedMessage, ex.Message);
    }

    [Fact]
    public async Task Sync_WhenKepubEnabledAndMissing_EnqueuesBackgroundKepubify()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Enqueue Kepub Series", "1");
        await context.SaveChangesAsync();

        var conversion = Substitute.For<IKoboConversionService>();
        conversion.TryGetCachedKepubPath(Arg.Any<int>(), Arg.Any<MangaFile>()).Returns((string?)null);

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        await koboService.SyncLibraryAsync(token, null);

        await conversion.Received(1).EnqueueKepubifyIfNeededAsync(chapter.Id, Arg.Any<MangaFile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_WhenKepubDisabled_DoesNotEnqueueKepubify()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        await AddEpubChapter(context, library, "No Enqueue Series", "1");
        await context.SaveChangesAsync();

        var conversion = Substitute.For<IKoboConversionService>();
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: false);
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        await koboService.SyncLibraryAsync(token, null);

        await conversion.DidNotReceive().EnqueueKepubifyIfNeededAsync(Arg.Any<int>(), Arg.Any<MangaFile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Download_Kepub_ConvertsViaService_AndUsesBudget()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Kepub Convert Download", "1");
        await context.SaveChangesAsync();

        var kepubPath = Path.Combine(Path.GetTempPath(), $"kobo-kepub-dl-{Guid.NewGuid():N}.kepub.epub");
        await File.WriteAllTextAsync(kepubPath, "kepub-bytes");
        try
        {
            var conversion = Substitute.For<IKoboConversionService>();
            conversion.GetOrConvertKepubAsync(chapter.Id, Arg.Any<MangaFile>(), Arg.Any<string>(), 30,
                    Arg.Any<CancellationToken>())
                .Returns(kepubPath);

            await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com", convertBudget: 30,
                enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");
            var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
            var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
            var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

            var download = await koboService.GetDownloadAsync(token, uuid, "kepub");
            Assert.Equal(kepubPath, download.FilePath);
            await conversion.Received(1).GetOrConvertKepubAsync(chapter.Id, Arg.Any<MangaFile>(),
                Arg.Any<string>(), 30, Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(kepubPath)) File.Delete(kepubPath);
        }
    }

    [Fact]
    public async Task Download_Kepub_Unavailable_PropagatesFromConversionService()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Kepub Slow", "1");
        await context.SaveChangesAsync();

        var conversion = Substitute.For<IKoboConversionService>();
        conversion.GetOrConvertKepubAsync(Arg.Any<int>(), Arg.Any<MangaFile>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new KavitaException(KoboConversionService.ConvertUnavailableMessage));

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);

        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            koboService.GetDownloadAsync(token, uuid, "kepub"));
        Assert.Equal(KoboConversionService.ConvertUnavailableMessage, ex.Message);
    }

    [Fact]
    public async Task KepubSuccess_DropsSyncedSet_SoNextSyncReoffersKepubOnly()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        var chapter = await AddEpubChapter(context, library, "Reoffer Series", "1");
        await context.SaveChangesAsync();
        chapter = await context.Chapter.Include(c => c.Files).SingleAsync(c => c.Id == chapter.Id);
        var source = chapter.Files.Single(f => f.Format == MangaFormat.Epub);

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com",
            enableKepubConversion: true, kepubifyPath: "/usr/bin/kepubify");

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-reoffer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var kepubify = Substitute.For<IKepubifyRunner>();
        kepubify.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var output = ci.ArgAt<string>(2);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "kepub-bytes");
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var conversion = new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            Substitute.For<IKoboArchiveEpubConverter>(),
            kepubify,
            Substitute.For<IKoboConversionJobScheduler>(),
            unitOfWork,
            Substitute.For<IEventHub>());
        KoboConversionService.ResetInFlightForTests();

        // First sync: EPUB fallback, chapter enters synced-set, enqueue would fire (real conversion).
        var koboService = CreateKoboService(unitOfWork, mapper, conversionService: conversion);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();
        var firstSync = await koboService.SyncLibraryAsync(token, null);
        var firstUrls = firstSync.Items[0]!["NewEntitlement"]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Equal(2, firstUrls.Count);
        Assert.Equal(1, await context.AppUserKoboSyncedChapter.CountAsync(s =>
            s.AppUserId == user.Id && s.ChapterId == chapter.Id));

        // KEPUB lands (background / download / warm-up path).
        await conversion.GetOrConvertKepubAsync(chapter.Id, source, "Title", budgetSeconds: 30);
        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s =>
            s.AppUserId == user.Id && s.ChapterId == chapter.Id));

        // Next sync re-emits with KEPUB-only DownloadUrls.
        var secondSync = await koboService.SyncLibraryAsync(token, firstSync.SyncToken);
        Assert.NotEmpty(secondSync.Items);
        var entitlementKey = secondSync.Items[0]!.AsObject().ContainsKey("NewEntitlement")
            ? "NewEntitlement"
            : "ChangedEntitlement";
        var secondUrls = secondSync.Items[0]![entitlementKey]!["BookMetadata"]!["DownloadUrls"]!.AsArray();
        Assert.Single(secondUrls);
        Assert.Equal(KoboService.KepubFormat, secondUrls[0]!["Format"]!.GetValue<string>());
    }
}
