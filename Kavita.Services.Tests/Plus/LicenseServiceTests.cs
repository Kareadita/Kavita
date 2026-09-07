using EasyCaching.Core;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Plus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Plus;

#nullable enable

/// <summary>
/// Exercises the failure branches of <see cref="LicenseService.GetLicenseInfo"/> against mocked
/// collaborators, without touching the Kavita+ HTTP boundary.
/// </summary>
public class LicenseServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private const string LicenseKey = "encrypted-license-key";

    private ILogger<LicenseService> _logger = null!;
    private IEasyCachingProviderFactory _cacheFactory = null!;
    private IKavitaPlusApiService _kavitaPlusApiService = null!;

    private LicenseService CreateService(IUnitOfWork unitOfWork)
    {
        _logger = Substitute.For<ILogger<LicenseService>>();

        var cacheProvider = Substitute.For<IEasyCachingProvider>();
        // No cache hits: every check in these tests is an upstream call
        cacheProvider.GetAsync<LicenseInfoDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CacheValue<LicenseInfoDto>(null, false));
        cacheProvider.GetAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CacheValue<bool>(false, false));

        _cacheFactory = Substitute.For<IEasyCachingProviderFactory>();
        _cacheFactory.GetCachingProvider(Arg.Any<string>())
            .Returns(cacheProvider);

        _kavitaPlusApiService = Substitute.For<IKavitaPlusApiService>();

        return new LicenseService(
            _cacheFactory,
            unitOfWork,
            _logger,
            Substitute.For<IVersionUpdaterService>(),
            _kavitaPlusApiService,
            Substitute.For<IFileCacheService>(),
            Substitute.For<IEventHub>());
    }

    private static async Task SaveLicenseKey(DataContext context, string value)
    {
        var setting = await context.ServerSetting
            .SingleAsync(s => s.Key == ServerSettingKey.LicenseKey);
        setting.Value = value;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetLicenseInfo_WithSavedLicense_AndNullUpstreamResponse_LogsWarning()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await SaveLicenseKey(context, LicenseKey);
        var service = CreateService(unitOfWork);

        _kavitaPlusApiService.GetLicenseInfo(Arg.Any<CancellationToken>())
            .Returns((LicenseInfoDto?) null);

        // Act
        var result = await service.GetLicenseInfo();

        // Assert - the previously silent branch is now observable
        Assert.Null(result);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Support Token")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetLicenseInfo_WithoutSavedLicense_DoesNotLog()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await SaveLicenseKey(context, string.Empty);
        var service = CreateService(unitOfWork);

        // Act
        var result = await service.GetLicenseInfo();

        // Assert - no license means nothing to warn about, and the API is never called
        Assert.Null(result);
        await _kavitaPlusApiService.DidNotReceiveWithAnyArgs().GetLicenseInfo(default);
        _logger.DidNotReceiveWithAnyArgs().Log(
            Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetLicenseInfo_FromCache_DoesNotLog()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await SaveLicenseKey(context, LicenseKey);
        var service = CreateService(unitOfWork);

        var cacheProvider = Substitute.For<IEasyCachingProvider>();
        cacheProvider.GetAsync<LicenseInfoDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CacheValue<LicenseInfoDto>(new LicenseInfoDto(), true));
        _cacheFactory.GetCachingProvider(Arg.Any<string>())
            .Returns(cacheProvider);

        // Act
        var result = await service.GetLicenseInfo();

        // Assert - a cached value short-circuits before any upstream call or warning
        Assert.NotNull(result);
        await _kavitaPlusApiService.DidNotReceiveWithAnyArgs().GetLicenseInfo(default);
        _logger.DidNotReceiveWithAnyArgs().Log(
            Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetLicenseInfo_WithValidUpstreamResponse_DoesNotLogWarning()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await SaveLicenseKey(context, LicenseKey);
        var service = CreateService(unitOfWork);

        _kavitaPlusApiService.GetLicenseInfo(Arg.Any<CancellationToken>())
            .Returns(new LicenseInfoDto { IsActive = true });

        // Act
        var result = await service.GetLicenseInfo();

        // Assert - the healthy path stays silent at Warning and returns the enriched info
        Assert.NotNull(result);
        Assert.True(result.IsActive);
        _logger.DidNotReceiveWithAnyArgs().Log(
            LogLevel.Warning, Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }
}
