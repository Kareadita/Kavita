using System.IO.Abstractions;
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
using Kavita.Models.DTOs.Settings;
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
}
