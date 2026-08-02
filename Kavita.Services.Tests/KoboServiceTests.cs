using System.IO;
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
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class KoboServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private readonly string _epubPath = Path.Join(Directory.GetCurrentDirectory(), "Data", "AesopsFables.epub");

    private static SettingsService CreateSettingsService(IUnitOfWork unitOfWork)
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        return new SettingsService(unitOfWork, ds, Substitute.For<ILibraryWatcher>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SettingsService>>(),
            Substitute.For<IOidcService>(), Substitute.For<ILoggingService>());
    }

    private static KoboService CreateKoboService(IUnitOfWork unitOfWork, IMapper mapper,
        string? coverImageDirectory = null)
    {
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.CoverImageDirectory.Returns(coverImageDirectory ?? Path.GetTempPath());
        return new KoboService(unitOfWork, Substitute.For<IAuthKeyService>(), Substitute.For<IEventHub>(), mapper,
            directoryService, new DownloadService());
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

        var downloadUrl = metadata["DownloadUrls"]![0]!;
        Assert.Equal(KoboService.EpubFormat, downloadUrl["Format"]!.GetValue<string>());
        Assert.Equal(
            $"https://kavita.example.com/base/api/kobo/{token}/download/{expectedUuid}/epub",
            downloadUrl["Url"]!.GetValue<string>());

        Assert.Equal(1, await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));

        // Second sync is empty (durable cursor) but still round-trips synctoken
        var page2 = await koboService.SyncLibraryAsync(token, result.SyncToken);
        Assert.Empty(page2.Items);
        Assert.False(page2.Continue);
        Assert.False(string.IsNullOrWhiteSpace(page2.SyncToken));
        _ = library;
    }

    [Fact]
    public async Task SyncLibrary_PaginatesAt100_WithContinueHeaderFlag()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SeedUser(unitOfWork, context);
        var library = await context.Library.SingleAsync(l => l.Name == "Kobo Lib");
        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        for (var i = 0; i < KoboService.SyncItemLimit + 5; i++)
        {
            await AddEpubChapter(context, library, $"Series {i}", "1");
        }

        await context.SaveChangesAsync();

        var page1 = await koboService.SyncLibraryAsync(token, null);
        Assert.Equal(KoboService.SyncItemLimit, page1.Items.Count);
        Assert.True(page1.Continue);

        var page2 = await koboService.SyncLibraryAsync(token, page1.SyncToken);
        Assert.Equal(5, page2.Items.Count);
        Assert.False(page2.Continue);

        Assert.Equal(KoboService.SyncItemLimit + 5,
            await context.AppUserKoboSyncedChapter.CountAsync(s => s.AppUserId == user.Id));
    }

    [Fact]
    public async Task SyncLibrary_Eligibility_RequiresAllowKoboSyncAndNativeEpub()
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
        await AddChapterWithFormat(context, allowed, "Pdf Book", "1", MangaFormat.Pdf);
        await AddChapterWithFormat(context, disallowed, "Disallowed Epub", "1", MangaFormat.Epub);
        await context.SaveChangesAsync();

        await ConfigureKoboSettings(unitOfWork, "https://kavita.example.com");
        var koboService = CreateKoboService(unitOfWork, mapper);
        var token = (await koboService.GetOrCreateSyncUrlAsync(user.Id)).Split('/').Last();

        var result = await koboService.SyncLibraryAsync(token, null);
        Assert.Single(result.Items);
        var uuid = result.Items[0]!["NewEntitlement"]!["BookEntitlement"]!["Id"]!.GetValue<string>();
        Assert.Equal(KoboEntitlementId.FromChapterIdString(epubChapter.Id), uuid);
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
        Assert.Single(downloadUrls);
        Assert.Equal(KoboService.EpubFormat, downloadUrls[0]!["Format"]!.GetValue<string>());
        Assert.Equal(1234, downloadUrls[0]!["Size"]!.GetValue<long>());

        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var download = await koboService.GetDownloadAsync(token, uuid, "epub");
        Assert.Equal(_epubPath, download.FilePath);
        Assert.Equal("application/epub+zip", download.ContentType);

        var metadata = await koboService.GetMetadataAsync(token, uuid);
        Assert.Equal(KoboService.EpubFormat, metadata[0]["DownloadUrls"]![0]!["Format"]!.GetValue<string>());
        Assert.StartsWith("https://kavita.example.com/api/kobo/",
            metadata[0]["DownloadUrls"]![0]!["Url"]!.GetValue<string>());
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
}
