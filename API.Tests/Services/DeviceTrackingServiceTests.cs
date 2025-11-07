using System;
using System.Threading;
using System.Threading.Tasks;
using API.Constants;
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

namespace API.Tests.Services;
#nullable enable


public class DeviceTrackingServiceTests : AbstractDbTest
{
    private readonly ILogger<DeviceTrackingService> _logger;
    private readonly HybridCache _cache;

    public DeviceTrackingServiceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _logger = Substitute.For<ILogger<DeviceTrackingService>>();
        _cache = Substitute.For<HybridCache>();
    }

    #region TrackDeviceAsync Tests

    [Fact]
    public async Task TrackDeviceAsync_ReturnsDeviceId_FromClientDeviceService()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var expectedDevice = CreateDevice(user.Id, 123);

        // Setup cache to miss and invoke the factory
        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<(int, ClientInfoData, string?, IClientDeviceService)>(),
                Arg.Any<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var state = callInfo.ArgAt<(int, ClientInfoData, string?, IClientDeviceService)>(1);
                var factory = callInfo.ArgAt<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(2);
                return factory(state, CancellationToken.None);
            });

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

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 123);


        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<(int, ClientInfoData, string?, IClientDeviceService)>(),
                Arg.Any<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
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
        await service.TrackDeviceAsync(user.Id, clientInfo, "device-123", CancellationToken.None);

        // Assert - Verify cache was called with correct key
        await _cache.Received(1).GetOrCreateAsync(
            $"device_tracking_{user.Id}_device-123",
            Arg.Any<object?>(),
            Arg.Any<Func<object?, CancellationToken, ValueTask<int>>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<string[]?>(),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task TrackDeviceAsync_UsesUnknownInCacheKey_WhenClientDeviceIdNull()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 456);

        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<(int, ClientInfoData, string?, IClientDeviceService)>(),
                Arg.Any<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
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
        await service.TrackDeviceAsync(user.Id, clientInfo, null, CancellationToken.None);

        // Assert - Verify "unknown" is used when clientDeviceId is null
        await _cache.Received(1).GetOrCreateAsync(
            $"device_tracking_{user.Id}_unknown",
            Arg.Any<object>(),
            Arg.Any<Func<object, CancellationToken, ValueTask<int>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackDeviceAsync_UsesUnknownInCacheKey_WhenClientDeviceIdEmpty()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 789);

        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<(int, ClientInfoData, string?, IClientDeviceService)>(),
                Arg.Any<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
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
        await service.TrackDeviceAsync(user.Id, clientInfo, string.Empty, CancellationToken.None);

        // Assert - Verify "unknown" is used when clientDeviceId is empty
        await _cache.Received(1).GetOrCreateAsync(
            $"device_tracking_{user.Id}_unknown",
            Arg.Any<object>(),
            Arg.Any<Func<object, CancellationToken, ValueTask<int>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackDeviceAsync_StoresReverseMappingInCache()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 999);

        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<(int, ClientInfoData, string?, IClientDeviceService)>(),
                Arg.Any<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
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
        await service.TrackDeviceAsync(user.Id, clientInfo, "device-xyz", CancellationToken.None);

        // Assert - Verify reverse mapping is stored: deviceId -> cacheKey
        await _cache.Received(1).SetAsync(
            $"device_key_mapping_{device.Id}",
            $"device_tracking_{user.Id}_device-xyz",
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackDeviceAsync_PropagatesCancellationToken()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo();
        var device = CreateDevice(user.Id, 111);
        var cts = new CancellationTokenSource();

        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<(int, ClientInfoData, string?, IClientDeviceService)>(),
                Arg.Any<Func<(int, ClientInfoData, string?, IClientDeviceService), CancellationToken, ValueTask<int>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
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
        await _cache.Received(1).GetOrCreateAsync(
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

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var deviceId = 123;
        var cacheKey = "device_tracking_1_device-123";

        // Setup cache to return the mapping key
        _cache.GetOrCreateAsync<string?>(
            $"device_key_mapping_{deviceId}",
            Arg.Any<Func<CancellationToken, ValueTask<string?>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(cacheKey);

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - Both cache entries should be removed
        await _cache.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"device_key_mapping_{deviceId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_HandlesNullCacheKey_Gracefully()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var deviceId = 456;

        // Setup cache to return null (no mapping found)
        _cache.GetOrCreateAsync<string?>(
            $"device_key_mapping_{deviceId}",
            Arg.Any<Func<CancellationToken, ValueTask<string?>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - Should only remove mapping key, not the null cache key
        await _cache.DidNotReceive().RemoveAsync(Arg.Is<string>(s => s != $"device_key_mapping_{deviceId}"), Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"device_key_mapping_{deviceId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_HandlesEmptyCacheKey_Gracefully()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var deviceId = 789;

        // Setup cache to return empty string
        _cache.GetOrCreateAsync<string?>(
            $"device_key_mapping_{deviceId}",
            Arg.Any<Func<CancellationToken, ValueTask<string?>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        // Act
        await service.ClearDeviceCacheAsync(deviceId);

        // Assert - Should only remove mapping key
        await _cache.Received(1).RemoveAsync($"device_key_mapping_{deviceId}", Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveAsync(string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearDeviceCacheAsync_LogsDebug_OnSuccess()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        const int deviceId = 999;
        const string cacheKey = "device_tracking_1_device-999";

        _cache.GetOrCreateAsync<string?>(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, ValueTask<string?>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(cacheKey);

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

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        const int deviceId = 111;

        // Setup cache to throw exception
        _cache.GetOrCreateAsync<string?>(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, ValueTask<string?>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns<string?>(_ => throw new InvalidOperationException("Cache error"));

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

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Create reading sessions with device IDs
        var session1 = CreateReadingSession(user.Id, [1, 2]);
        var session2 = CreateReadingSession(user.Id, [3, 2]); // Device 2 appears twice

        context.AppUserReadingSession.AddRange(session1, session2);
        await context.SaveChangesAsync();

        _cache.GetOrCreateAsync<string?>(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, ValueTask<string?>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns("some-cache-key");

        // Act
        await service.ClearUserDeviceCachesAsync(user.Id);

        // Assert - Should clear cache for devices 1, 2, and 3 (distinct)
        await _cache.Received().RemoveAsync("some-cache-key", Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync("device_key_mapping_1", Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync("device_key_mapping_2", Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync("device_key_mapping_3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearUserDeviceCachesAsync_HandlesUserWithNoSessions()
    {

        var (_, context, mapper) = await CreateDatabase();
        var clientDeviceService = Substitute.For<IClientDeviceService>();
        var service = new DeviceTrackingService(_cache, context, _logger, clientDeviceService);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Act - User has no reading sessions
        await service.ClearUserDeviceCachesAsync(user.Id);

        // Assert - Should not throw, and not call cache remove
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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

    private static AppUserReadingSession CreateReadingSession(int userId, int[] deviceIds)
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
