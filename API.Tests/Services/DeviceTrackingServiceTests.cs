using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Entities.User;
using API.Helpers.Builders;
using API.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using API.Entities;

namespace API.Tests.Services;
#nullable enable


public class DeviceTrackingServiceTests : AbstractDbTest
{
    private readonly ILogger<DeviceTrackingService> _logger;

    public DeviceTrackingServiceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _logger = Substitute.For<ILogger<DeviceTrackingService>>();
    }

    #region TrackDeviceAsync Tests

    [Fact]
    public async Task TrackDeviceAsync_ReturnsDeviceId_FromClientDeviceService()
    {
        var cache = new FakeHybridCache();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var expectedDevice = CreateDevice(user.Id, 123);

        clientDeviceService.IdentifyOrRegisterDeviceAsync(
                user.Id,
                clientInfo,
                "device-123",
                Arg.Any<CancellationToken>())
            .Returns(expectedDevice);

        // Act
        var deviceId = await service.TrackDeviceAsync(user.Id, clientInfo, "device-123", CancellationToken.None);

        // Assert
        Assert.Equal(123, deviceId);
        await clientDeviceService.Received(1).IdentifyOrRegisterDeviceAsync(
            user.Id,
            clientInfo,
            "device-123",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackDeviceAsync_CachesDeviceId_WithCorrectKey()
    {
        var cache = new FakeHybridCacheWithTracking();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 123);

        clientDeviceService.IdentifyOrRegisterDeviceAsync(
                Arg.Any<int>(),
                Arg.Any<ClientInfoData>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        await service.TrackDeviceAsync(user.Id, clientInfo, "device-123", CancellationToken.None);

        // Assert - Verify cache was called with correct key
        Assert.Single(cache.GetOrCreateAsyncCalls.Where(call =>
            call.Key == $"device_tracking_{user.Id}_device-123"));
    }


    [Fact]
    public async Task TrackDeviceAsync_UsesUnknownInCacheKey_WhenClientDeviceIdNull()
    {
        var cache = new FakeHybridCacheWithTracking();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 456);

        clientDeviceService.IdentifyOrRegisterDeviceAsync(
                Arg.Any<int>(),
                Arg.Any<ClientInfoData>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        await service.TrackDeviceAsync(user.Id, clientInfo, null, CancellationToken.None);

        // Assert - Verify cache key is generated correctly when clientDeviceId is null
        Assert.Single(cache.GetOrCreateAsyncCalls, call =>
            call.Key == $"device_tracking_{user.Id}_Chrome");
    }

    [Fact]
    public async Task TrackDeviceAsync_UsesUnknownInCacheKey_WhenClientDeviceIdEmpty()
    {
        var cache = new FakeHybridCacheWithTracking();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 789);

        clientDeviceService.IdentifyOrRegisterDeviceAsync(
                Arg.Any<int>(),
                Arg.Any<ClientInfoData>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        await service.TrackDeviceAsync(user.Id, clientInfo, string.Empty, CancellationToken.None);

        // Assert - Verify cache key is generated correctly when clientDeviceId is empty
        Assert.Single(cache.GetOrCreateAsyncCalls, call =>
            call.Key == $"device_tracking_{user.Id}_Chrome");
    }

    [Fact]
    public async Task TrackDeviceAsync_StoresReverseMappingInCache()
    {
        var cache = new FakeHybridCacheWithTracking();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 999);

        clientDeviceService.IdentifyOrRegisterDeviceAsync(
                Arg.Any<int>(),
                Arg.Any<ClientInfoData>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        await service.TrackDeviceAsync(user.Id, clientInfo, "device-xyz", CancellationToken.None);

        // Assert - Verify reverse mapping is stored: deviceId -> cacheKey
        Assert.Single(cache.SetAsyncCalls, call =>
            call.Key == $"device_key_mapping_{device.Id}");
        Assert.True(cache.ContainsKey($"device_key_mapping_{device.Id}"));
        Assert.Equal($"device_tracking_{user.Id}_device-xyz",
            await cache.GetOrCreateAsync($"device_key_mapping_{device.Id}",
                _ => ValueTask.FromResult(string.Empty)));
    }

    [Fact]
    public async Task TrackDeviceAsync_PropagatesCancellationToken()
    {

        var cache = Substitute.For<HybridCache>();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 111);
        var cts = new CancellationTokenSource();

        cache.GetOrCreateAsync<(int, ClientInfoData, string?, IClientDeviceService), int>(
                default,
                default,
                default!,
                default,
                default,
                default)
            .ReturnsForAnyArgs(callInfo =>
            {
                var state = callInfo.ArgAt<(int, ClientInfoData, string?, IClientDeviceService)>(1);
                var factory = callInfo.ArgAt<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(2);
                return factory(state, CancellationToken.None);
            });

        clientDeviceService.IdentifyOrRegisterDeviceAsync(
            Arg.Any<int>(),
            Arg.Any<ClientInfoData>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        await service.TrackDeviceAsync(user.Id, clientInfo, "device-abc", cts.Token);

        // Assert - Verify CancellationToken was propagated
        await cache.Received(1).GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<Func<object, CancellationToken, ValueTask<int>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            cts.Token);
    }

    #endregion

    #region ClearDeviceCacheAsync Tests

    [Fact]
    public async Task ClearDeviceCacheAsync_RemovesBothCacheEntries()
    {

        var cache = new FakeHybridCache();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var deviceId = 123;
        var cacheKey = "device_tracking_1_device-123";
        var mappingKey = $"device_key_mapping_{deviceId}";

        // Pre-seed the cache with both entries
        cache.Seed(mappingKey, cacheKey);
        cache.Seed(cacheKey, deviceId); // The actual device data (adjust type as needed)

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - Both cache entries should be removed
        Assert.False(cache.ContainsKey(cacheKey));
        Assert.False(cache.ContainsKey(mappingKey));
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_HandlesNullCacheKey_Gracefully()
    {

        var cache = Substitute.For<HybridCache>();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var deviceId = 456;

        // Setup cache to return null (no mapping found)
        cache.GetOrCreateAsync<string?>(
            default,
            default!,
            default,
            default,
            default)
            .ReturnsForAnyArgs((string?)null);

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - Should only remove mapping key, not the null cache key
        await cache.DidNotReceive().RemoveAsync(Arg.Is<string>(s => s != $"device_key_mapping_{deviceId}"), Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync($"device_key_mapping_{deviceId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_HandlesEmptyCacheKey_Gracefully()
    {

        var cache = new FakeHybridCache();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var deviceId = 789;

        // Pre-seed the cache with empty string for the mapping key
        cache.Seed($"device_key_mapping_{deviceId}", string.Empty);

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - mapping key should be removed, but no attempts to remove empty string key
        Assert.False(cache.ContainsKey($"device_key_mapping_{deviceId}"));
        Assert.False(cache.ContainsKey(string.Empty)); // Should never have been added
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_LogsDebug_OnSuccess()
    {

        var cache = Substitute.For<HybridCache>();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        const int deviceId = 999;
        const string cacheKey = "device_tracking_1_device-999";

        cache.GetOrCreateAsync<string?>(
            default,
            default!,
            default,
            default,
            default)
            .ReturnsForAnyArgs(cacheKey);

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert
        _logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains($"Cleared device cache for device {deviceId}")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_LogsWarning_OnException()
    {

        var cache = Substitute.For<HybridCache>();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        const int deviceId = 111;

        // Setup cache to throw exception
        cache.GetOrCreateAsync<string?>(
            default,
            default!,
            default,
            default,
            default)
            .ReturnsForAnyArgs<string?>(_ => throw new InvalidOperationException("Cache error"));

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - Should log warning and not throw
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains($"Failed to clear device cache for device {deviceId}")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region ClearUserDeviceCachesAsync Tests

    [Fact]
    public async Task ClearUserDeviceCachesAsync_ClearsAllDeviceCaches_ForUser()
    {
        var cache = new FakeHybridCache();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Make sure we mock up the Series/Chapter Id for the tracking
        var series = new SeriesBuilder("Spice and Wolf")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(series)
            .Build();

        user.Libraries.Add(library);
        await context.SaveChangesAsync();

        // Create devices first
        var device1 = CreateDevice(user.Id, 1);
        var device2 = CreateDevice(user.Id, 2);
        var device3 = CreateDevice(user.Id, 3);
        context.ClientDevice.AddRange(device1, device2, device3);
        await context.SaveChangesAsync();

        // Create reading sessions - but save them separately to isolate FK issues
        var session1 = CreateReadingSession(user.Id, [1, 2]);
        context.AppUserReadingSession.Add(session1);
        await context.SaveChangesAsync();

        var session2 = CreateReadingSession(user.Id, [3, 2]); // Device 2 appears twice
        context.AppUserReadingSession.Add(session2);
        await context.SaveChangesAsync();

        // Pre-seed cache with device mappings and their cache keys
        cache.Seed("device_key_mapping_1", "cache-key-1");
        cache.Seed("device_key_mapping_2", "cache-key-2");
        cache.Seed("device_key_mapping_3", "cache-key-3");
        cache.Seed("cache-key-1", 1);
        cache.Seed("cache-key-2", 2);
        cache.Seed("cache-key-3", 3);

        // Act
        await service.ClearUserDeviceCachesAsync(user.Id);

        // Assert - Should clear cache for devices 1, 2, and 3 (distinct)
        Assert.False(cache.ContainsKey("cache-key-1"));
        Assert.False(cache.ContainsKey("cache-key-2"));
        Assert.False(cache.ContainsKey("cache-key-3"));
        Assert.False(cache.ContainsKey("device_key_mapping_1"));
        Assert.False(cache.ContainsKey("device_key_mapping_2"));
        Assert.False(cache.ContainsKey("device_key_mapping_3"));
    }

    [Fact]
    public async Task ClearUserDeviceCachesAsync_HandlesUserWithNoSessions()
    {

        var cache = Substitute.For<HybridCache>();
        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Act - User has no reading sessions
        await service.ClearUserDeviceCachesAsync(user.Id);

        // Assert - Should not throw, and not call cache remove
        await cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper Methods

    private static ClientInfoData CreateClientInfo()
    {
        return new ClientInfoData
        {
            ClientType = ClientDeviceType.WebBrowser,
            Platform = ClientDevicePlatform.Windows,
            DeviceType = "Desktop",
            Browser = "Chrome",
            BrowserVersion = "120",
            UserAgent = "Test User Agent",
            IpAddress = "127.0.0.1",
            AuthType = AuthenticationType.JWT,
            CapturedAt = DateTime.UtcNow
        };
    }

    private static ClientDevice CreateDevice(int userId, int deviceId)
    {
        return new ClientDevice
        {
            Id = deviceId,
            AppUserId = userId,
            UiFingerprint = $"device-{deviceId}",
            DeviceFingerprint = Guid.NewGuid().ToString(),
            FriendlyName = "Test Device",
            CurrentClientInfo = new ClientInfoData
            {
                ClientType = ClientDeviceType.WebBrowser,
                Platform = ClientDevicePlatform.Windows,
                UserAgent = "Test",
                IpAddress = "127.0.0.1",
                CapturedAt = DateTime.UtcNow
            },
            FirstSeenUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow,
            IsActive = true
        };
    }

    private static AppUserReadingSession CreateReadingSession(int userId, List<int> deviceIds)
    {
        var session = new AppUserReadingSession
        {
            AppUserId = userId,
            StartTime = DateTime.Now,
            StartTimeUtc = DateTime.UtcNow,
            IsActive = true,
            ActivityData = []
        };

        session.ActivityData.Add(new AppUserReadingSessionActivityData
        {
            ChapterId = 1,
            VolumeId = 1,
            SeriesId = 1,
            LibraryId = 1,
            DeviceIds = deviceIds,
            StartPage = 0,
            EndPage = 10,
            StartTime = DateTime.Now,
            StartTimeUtc = DateTime.UtcNow,
            PagesRead = 10
        });

        return session;
    }

    #endregion
}
