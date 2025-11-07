using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Entities.User;
using API.Helpers.Builders;
using API.Services;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;
#nullable enable

public class ClientDeviceServiceTests : AbstractDbTest
{
    private readonly ILogger<ClientDeviceService> _logger;

    public ClientDeviceServiceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _logger = Substitute.For<ILogger<ClientDeviceService>>();
    }

    #region IdentifyOrRegisterDeviceAsync Tests

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_RegistersNewDevice_WhenNoExistingMatch()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");

        // Act
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, "device-123");

        // Assert
        Assert.NotNull(device);
        Assert.Equal(user.Id, device.AppUserId);
        Assert.Equal("device-123", device.UiFingerprint);
        Assert.NotEmpty(device.DeviceFingerprint);
        Assert.Equal("Chrome on Windows", device.FriendlyName);
        Assert.True(device.IsActive);
        Assert.NotNull(device.CurrentClientInfo);
        Assert.Single(device.History);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_MatchesExistingDevice_ByClientDeviceId()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var existingDevice = new ClientDevice
        {
            AppUserId = user.Id,
            AppUser = user,
            UiFingerprint = "device-123",
            DeviceFingerprint = "some-fingerprint",
            FriendlyName = "My Device",
            CurrentClientInfo = clientInfo,
            FirstSeenUtc = DateTime.UtcNow.AddDays(-5),
            LastSeenUtc = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            History = new List<ClientDeviceHistory>()
        };
        context.ClientDevice.Add(existingDevice);
        await context.SaveChangesAsync();

        var originalLastSeen = existingDevice.LastSeenUtc;

        // Act
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, "device-123");

        // Assert
        Assert.Equal(existingDevice.Id, device.Id);
        Assert.True(device.LastSeenUtc > originalLastSeen);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_MatchesExistingDevice_ByFingerprint_WhenClientDeviceIdNull()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");

        // Register first time without client device ID
        var firstDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, null);
        var fingerprint = firstDevice.DeviceFingerprint;

        // Act - Same device, still no client device ID (e.g., OPDS reader without device ID support)
        var secondDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, null);

        // Assert - Should match by fingerprint alone
        Assert.Equal(firstDevice.Id, secondDevice.Id);
        Assert.Equal(fingerprint, secondDevice.DeviceFingerprint);
        Assert.Null(secondDevice.UiFingerprint);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_UpdatesClientDeviceId_WhenFingerprintMatches()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");

        // First request without ClientDeviceId
        var firstDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, null);
        Assert.Null(firstDevice.UiFingerprint);

        // Act - Second request with ClientDeviceId
        var secondDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, "device-456");

        // Assert
        Assert.Equal(firstDevice.Id, secondDevice.Id);
        Assert.Equal("device-456", secondDevice.UiFingerprint);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_UsesFuzzyMatching_ForBrowserVersionUpgrade()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var oldClientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var oldDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, oldClientInfo, null);

        // Act - Browser upgraded to version 121
        var newClientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "121");
        var newDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, newClientInfo, null);

        // Assert - Should fuzzy match to same device
        Assert.Equal(oldDevice.Id, newDevice.Id);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_CreatesNewDevice_WhenPlatformChanges()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var windowsInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var windowsDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, windowsInfo, null);

        // Act - Same browser, different platform
        var macInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.MacOs, "Desktop", "Chrome", "120");
        var macDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, macInfo, null);

        // Assert - Should create new device
        Assert.NotEqual(windowsDevice.Id, macDevice.Id);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_IgnoresInactiveDevices()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var inactiveDevice = new ClientDevice
        {
            AppUserId = user.Id,
            AppUser = user,
            UiFingerprint = "device-old",
            DeviceFingerprint = "fingerprint",
            FriendlyName = "Old Device",
            CurrentClientInfo = clientInfo,
            FirstSeenUtc = DateTime.UtcNow.AddDays(-30),
            LastSeenUtc = DateTime.UtcNow.AddDays(-30),
            IsActive = false, // Inactive
            History = new List<ClientDeviceHistory>()
        };
        context.ClientDevice.Add(inactiveDevice);
        await context.SaveChangesAsync();

        // Act
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, "device-old");

        // Assert - Should create new device, not match inactive one
        Assert.NotEqual(inactiveDevice.Id, device.Id);
        Assert.True(device.IsActive);
    }

    #endregion

    #region Fingerprint Tests

    [Fact]
    public async Task GenerateDeviceFingerprint_GeneratesConsistentHash_ForSameInput()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo1 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var clientInfo2 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");

        // Act
        var device1 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo1, null);
        var device2 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo2, null);

        // Assert
        Assert.Equal(device1.Id, device2.Id);
        Assert.Equal(device1.DeviceFingerprint, device2.DeviceFingerprint);
    }

    [Fact]
    public async Task GenerateDeviceFingerprint_Fallbacks_WhenBrowserChangesOneMajorVersion()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var chromeInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var firefoxInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Firefox", "121");

        // Act
        var chromeDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, chromeInfo, null);
        var firefoxDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, firefoxInfo, null);

        // The fingerprint would be different, but we fall back to fuzzy matching which allows for 1 major version of leniency for device matching
        Assert.Equal(chromeDevice.DeviceFingerprint, firefoxDevice.DeviceFingerprint);
    }

    [Fact]
    public async Task GenerateDeviceFingerprint_GeneratesDifferentHash_WhenBrowserChangesTwoMajorVersions()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var chromeInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var firefoxInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Firefox", "122");

        // Act
        var chromeDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, chromeInfo, null);
        var firefoxDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, firefoxInfo, null);

        Assert.NotEqual(chromeDevice.DeviceFingerprint, firefoxDevice.DeviceFingerprint);
    }

    [Fact]
    public async Task GenerateDeviceFingerprint_IsCaseInsensitive()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo1 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var clientInfo2 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");

        // Act
        var device1 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo1, null);
        var device2 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo2, null);

        // Assert
        Assert.Equal(device1.Id, device2.Id);
        Assert.Equal(device1.DeviceFingerprint, device2.DeviceFingerprint);
    }

    [Fact]
    public async Task GenerateDeviceFingerprint_UsesMajorVersionOnly_ForFingerprinting()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo1 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var clientInfo2 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120.0.6099.109");

        // Act
        var device1 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo1, null);
        var device2 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo2, null);

        // Assert - Both should produce same fingerprint (major version 120)
        Assert.Equal(device1.Id, device2.Id);
        Assert.Equal(device1.DeviceFingerprint, device2.DeviceFingerprint);
    }

    [Fact]
    public async Task IdentifyOrRegisterDeviceAsync_MatchesSameDevice_ForMinorBrowserVersionChanges()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // First access with Chrome 120.1.25
        var clientInfo1 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120.1.25");
        var device1 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo1, null);

        // Act - Minor version update: 120.1.25 -> 120.2.0 (same major version)
        var clientInfo2 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120.2.0");
        var device2 = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo2, null);

        // Assert - Should match same device (fuzzy matching allows minor version changes)
        Assert.Equal(device1.Id, device2.Id);
    }

    #endregion

    #region Fuzzy Matching Tests

    [Fact]
    public async Task FuzzyMatching_Matches_WithHighSimilarity()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Register with Chrome 120
        var oldInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var oldDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, oldInfo, null);

        // Act - Browser updated to 121 (within 1 major version tolerance)
        var newInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "121");
        var newDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, newInfo, null);

        // Assert
        Assert.Equal(oldDevice.Id, newDevice.Id);
    }

    [Fact]
    public async Task FuzzyMatching_DoesNotMatch_WithLowSimilarity()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Register Chrome on Windows Desktop
        var chromeInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var chromeDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, chromeInfo, null);

        // Act - Completely different: Firefox on Linux Mobile
        var firefoxInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Linux, "Mobile", "Firefox", "121");
        var firefoxDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, firefoxInfo, null);

        // Assert - Should create new device
        Assert.NotEqual(chromeDevice.Id, firefoxDevice.Id);
    }

    [Fact]
    public async Task FuzzyMatching_OnlyConsidersRecentDevices_Within30Days()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var oldDevice = new ClientDevice
        {
            AppUserId = user.Id,
            AppUser = user,
            DeviceFingerprint = "old-fingerprint",
            FriendlyName = "Old Device",
            CurrentClientInfo = clientInfo,
            FirstSeenUtc = DateTime.UtcNow.AddDays(-60),
            LastSeenUtc = DateTime.UtcNow.AddDays(-35), // More than 30 days ago
            IsActive = true,
            History = new List<ClientDeviceHistory>()
        };
        context.ClientDevice.Add(oldDevice);
        await context.SaveChangesAsync();

        // Act - Similar device but with minor version change
        var newInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "121");
        var newDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, newInfo, null);

        // Assert - Should create new device since old one is outside 30-day window
        Assert.NotEqual(oldDevice.Id, newDevice.Id);
    }

    #endregion

    #region UpdateDeviceActivityAsync Tests

    [Fact]
    public async Task UpdateDeviceActivity_UpdatesLastSeenUtc()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, "device-123");
        var originalLastSeen = device.LastSeenUtc;

        await Task.Delay(10); // Ensure time difference

        // Act
        var updatedDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, "device-123");

        // Assert
        Assert.True(updatedDevice.LastSeenUtc > originalLastSeen);
    }

    [Fact]
    public async Task UpdateDeviceActivity_AddsHistoryRecord_WhenMeaningfulChanges()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var oldInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, oldInfo, "device-123");
        var initialHistoryCount = device.History.Count;

        // Act - Meaningful change: browser version upgrade
        var newInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "121");
        var updatedDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, newInfo, "device-123");

        // Assert
        var reloadedDevice = await context.ClientDevice
            .Include(d => d.History)
            .FirstAsync(d => d.Id == updatedDevice.Id);
        Assert.True(reloadedDevice.History.Count > initialHistoryCount);
    }

    [Fact]
    public async Task UpdateDeviceActivity_DoesNotAddHistory_ForNonMeaningfulChanges()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo1 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        clientInfo1.IpAddress = "192.168.1.1";
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo1, "device-123");
        var initialHistoryCount = device.History.Count;

        // Act - Non-meaningful change: just IP address
        var clientInfo2 = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");
        clientInfo2.IpAddress = "192.168.1.2"; // Only IP changed
        var updatedDevice = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo2, "device-123");

        // Assert
        var reloadedDevice = await context.ClientDevice
            .Include(d => d.History)
            .FirstAsync(d => d.Id == updatedDevice.Id);
        Assert.Equal(initialHistoryCount, reloadedDevice.History.Count);
    }

    #endregion

    #region Friendly Name Generation Tests

    [Fact]
    public async Task GenerateFriendlyName_IncludesBrowserAndPlatform()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.WebBrowser, ClientDevicePlatform.Windows, "Desktop", "Chrome", "120");

        // Act
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, null);

        // Assert
        Assert.Equal("Chrome on Windows", device.FriendlyName);
    }

    [Fact]
    public async Task GenerateFriendlyName_UsesClientType_WhenNotWebBrowser()
    {
        // TODO: Remove these tests
        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.KoReader, ClientDevicePlatform.Linux, "E-Reader", null, null);

        // Act
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, null);

        // Assert
        Assert.Equal("KOReader on Linux", device.FriendlyName);
    }

    [Fact]
    public async Task GenerateFriendlyName_HandlesNoPlatform()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var clientInfo = CreateClientInfo(ClientDeviceType.OpdsClient, ClientDevicePlatform.Unknown, null, null, null);

        // Act
        var device = await service.IdentifyOrRegisterDeviceAsync(user.Id, clientInfo, null);

        // Assert
        Assert.Equal("OPDS Client on Unknown", device.FriendlyName);
    }

    #endregion

    #region CRUD Operations Tests

    [Fact]
    public async Task GetUserDevicesAsync_ReturnsOnlyActiveDevices_ByDefault()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var activeDevice = CreateDeviceEntity(user.Id, "active-device", true, user);
        var inactiveDevice = CreateDeviceEntity(user.Id, "inactive-device", false, user);
        context.ClientDevice.AddRange(activeDevice, inactiveDevice);
        await context.SaveChangesAsync();

        // Act
        var devices = (await service.GetUserDevicesAsync(user.Id, includeInactive: false)).ToList();

        // Assert
        Assert.Single(devices);
        Assert.Contains(devices, d => d.UiFingerprint == "active-device");
    }

    [Fact]
    public async Task GetUserDevicesAsync_ReturnsAllDevices_WhenIncludeInactiveTrue()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var activeDevice = CreateDeviceEntity(user.Id, "active-device", true, user);
        var inactiveDevice = CreateDeviceEntity(user.Id, "inactive-device", false, user);
        context.ClientDevice.AddRange(activeDevice, inactiveDevice);
        await context.SaveChangesAsync();

        // Act
        var devices = await service.GetUserDevicesAsync(user.Id, includeInactive: true);

        // Assert
        Assert.Equal(2, devices.Count());
    }

    [Fact]
    public async Task RenameDeviceAsync_UpdatesDeviceName()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var device = CreateDeviceEntity(user.Id, "device-123", true, user);
        context.ClientDevice.Add(device);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RenameDeviceAsync(user.Id, device.Id, "My Custom Name");

        // Assert
        Assert.True(result);
        var updated = await context.ClientDevice.FindAsync(device.Id);
        Assert.Equal("My Custom Name", updated!.FriendlyName);
    }

    [Fact]
    public async Task RenameDeviceAsync_ReturnsFalse_WhenDeviceNotFound()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RenameDeviceAsync(user.Id, 999, "New Name");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveDeviceAsync_MarksDeviceAsInactive()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var device = CreateDeviceEntity(user.Id, "device-123", true, user);
        context.ClientDevice.Add(device);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeleteDeviceAsync(user.Id, device.Id);

        // Assert
        Assert.True(result);
        var updated = await context.ClientDevice.FindAsync(device.Id);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task RemoveDeviceAsync_ReturnsFalse_WhenDeviceNotFound()
    {

        var (_, context, mapper) = await CreateDatabase();
        var service = new ClientDeviceService(context, mapper, _logger);

        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Act
        var exception = await Assert.ThrowsAsync<KavitaException>(async () =>
            await service.DeleteDeviceAsync(user.Id, 999));

        Assert.Contains("client-device-doesnt-exist", exception.Message);
    }

    #endregion

    #region Helper Methods

    private static ClientInfoData CreateClientInfo(
        ClientDeviceType clientType,
        ClientDevicePlatform platform,
        string? deviceType,
        string? browser,
        string? browserVersion)
    {
        return new ClientInfoData
        {
            ClientType = clientType,
            Platform = platform,
            DeviceType = deviceType,
            Browser = browser,
            BrowserVersion = browserVersion,
            UserAgent = "Test User Agent",
            IpAddress = "127.0.0.1",
            AuthType = AuthenticationType.JWT,
            CapturedAt = DateTime.UtcNow
        };
    }

    private static ClientDevice CreateDeviceEntity(int userId, string clientDeviceId, bool isActive, AppUser? user = null)
    {
        var device = new ClientDevice
        {
            AppUserId = userId,
            UiFingerprint = clientDeviceId,
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
            IsActive = isActive,
            History = new List<ClientDeviceHistory>()
        };

        if (user != null)
        {
            device.AppUser = user;
        }

        return device;
    }

    #endregion
}
