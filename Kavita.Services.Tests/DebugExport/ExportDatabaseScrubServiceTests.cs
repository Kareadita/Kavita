using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Models.AutoMapper;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.DebugExport;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Kavita.Services.Tests.DebugExport;

public class ExportDatabaseScrubServiceTests : IDisposable
{
    private const string JoeApiKey = "joe-api-key-0123456789";
    private const string JoeIp = "82.123.6.194";
    private const string KindleEmail = "joe@kindle.example.com";
    private const string LicenseKey = "license-key-abcdef0123456789";
    private const string OidcSecret = "oidc-secret-0123456789";
    private const string MountFingerprint = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private readonly string _directory = Path.Join(Path.GetTempPath(), $"kavita_scrub_test_{Guid.NewGuid()}");
    private readonly string _snapshotPath;

    public ExportDatabaseScrubServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _snapshotPath = Path.Join(_directory, "kavita.db");
    }

    [Fact]
    public async Task EveryMarkedValueIsReplaced()
    {
        await SeedSnapshot();

        await CreateService().ScrubAsync(_snapshotPath);

        await using var context = OpenSnapshot();
        var joe = await context.AppUser.SingleAsync(u => u.Id == 1);
        var device = await context.Device.SingleAsync();
        var clientDevice = await context.ClientDevice.SingleAsync();
        var license = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.LicenseKey);
        var oidc = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.OidcConfiguration);

        Assert.NotEqual(JoeApiKey, joe.ApiKey);
        Assert.NotEqual(KindleEmail, device.EmailAddress);
        Assert.NotEqual(JoeIp, clientDevice.CurrentClientInfo.IpAddress);
        Assert.NotEqual(LicenseKey, license.Value);
        Assert.NotEqual(OidcSecret, JsonSerializer.Deserialize<OidcConfigDto>(oidc.Value)!.Secret);
    }

    [Fact]
    public async Task NullAndEmptyStayAsTheyAre()
    {
        await SeedSnapshot();

        await CreateService().ScrubAsync(_snapshotPath);

        await using var context = OpenSnapshot();
        var amelia = await context.AppUser.SingleAsync(u => u.Id == 2);
        var emailHost = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.EmailHost);

        Assert.Null(amelia.ApiKey);
        Assert.Equal(string.Empty, amelia.ConfirmationToken);
        Assert.Equal(string.Empty, emailHost.Value);
    }

    [Fact]
    public async Task OneOriginalGetsOneReplacement()
    {
        await SeedSnapshot();

        var result = await CreateService().ScrubAsync(_snapshotPath);

        await using var context = OpenSnapshot();
        var device = await context.Device.SingleAsync();
        var clientDevice = await context.ClientDevice.SingleAsync();

        Assert.True(result.Junk.Replaced.TryGetValue(JoeIp, out var junk));
        Assert.Equal(junk, device.IpAddress);
        Assert.Equal(junk, clientDevice.CurrentClientInfo.IpAddress);
    }

    [Fact]
    public async Task JsonScrubKeepsOtherFields()
    {
        await SeedSnapshot();

        await CreateService().ScrubAsync(_snapshotPath);

        await using var context = OpenSnapshot();
        var info = (await context.ClientDevice.SingleAsync()).CurrentClientInfo;

        Assert.NotEqual(JoeIp, info.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", info.UserAgent);
        Assert.Equal("Firefox", info.Browser);
    }

    [Fact]
    public async Task ScrubLeavesLastModifiedAlone()
    {
        await SeedSnapshot();
        DateTime before;
        await using (var context = OpenSnapshot())
        {
            before = (await context.Library.SingleAsync()).LastModified;
        }

        await CreateService().ScrubAsync(_snapshotPath);

        await using var after = OpenSnapshot();
        var library = await after.Library.SingleAsync();
        Assert.NotEqual(MountFingerprint, library.FolderWatchState.Mounts.Single().Fingerprint);
        Assert.Equal(before, library.LastModified);
    }

    [Fact]
    public async Task InjectedAdminCanSignIn()
    {
        await SeedSnapshot();
        int themesBefore;
        await using (var context = OpenSnapshot())
        {
            themesBefore = await context.SiteTheme.CountAsync();
        }

        var result = await CreateService().ScrubAsync(_snapshotPath);

        await using var after = OpenSnapshot();
        var admin = await after.AppUser
            .Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .Include(u => u.Libraries)
            .Include(u => u.AuthKeys)
            .Include(u => u.DashboardStreams)
            .Include(u => u.SideNavStreams)
            .Include(u => u.ReadingProfiles)
            .Include(u => u.UserPreferences)
            .AsSplitQuery()
            .SingleOrDefaultAsync(u => u.UserName == result.AdminUserName);

        Assert.NotNull(admin);
        Assert.DoesNotMatch(@"^user-\d+$", admin.UserName!);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<AppUser>().VerifyHashedPassword(admin, admin.PasswordHash!, result.AdminPassword));
        Assert.True(admin.EmailConfirmed);
        Assert.Contains(PolicyConstants.AdminRole, admin.UserRoles.Select(r => r.Role.Name));
        Assert.Contains(PolicyConstants.LoginRole, admin.UserRoles.Select(r => r.Role.Name));
        Assert.Equal(await after.Library.CountAsync(), admin.Libraries.Count);
        Assert.Contains(admin.AuthKeys, k => k.Key == result.AdminAuthKey);
        Assert.Equal(AgeRating.NotApplicable, admin.AgeRestriction);
        AccountServiceTests.AssertSeeded(admin);

        Assert.Equal(1, await after.Roles.CountAsync(r => r.Name == PolicyConstants.AdminRole));
        Assert.Equal(themesBefore, await after.SiteTheme.CountAsync());
    }

    private ExportDatabaseScrubService CreateService()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly)).CreateMapper();
        var accountService = new AccountService(null!, Substitute.For<ILogger<AccountService>>(), null!, mapper, null!);
        return new ExportDatabaseScrubService(Substitute.For<ILogger<ExportDatabaseScrubService>>(), accountService, mapper);
    }

    private DataContext OpenSnapshot() =>
        new(new DbContextOptionsBuilder<DataContext>().UseSqlite($"Data Source={_snapshotPath};Pooling=False").Options);

    /// <summary>
    /// Users 1 (joe, every secret filled) and 2 (amelia, null and empty), with one of each scrubbed kind around them
    /// </summary>
    private async Task SeedSnapshot()
    {
        await using var context = OpenSnapshot();
        await context.Database.EnsureCreatedAsync();
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.BackupDirectory.Returns(_directory);
        await Seed.SeedSettings(context, directoryService);
        await Seed.SeedThemes(context);
        foreach (var role in new[] { PolicyConstants.AdminRole, PolicyConstants.LoginRole })
        {
            context.Roles.Add(new AppRole { Name = role, NormalizedName = role.ToUpperInvariant() });
        }

        var series = new SeriesBuilder("Berserk").Build();
        var library = new LibraryBuilder("Manga").WithSeries(series).Build();
        library.FolderWatchState = new LibraryFolderWatchState
        {
            Mounts = [new LibraryFolderWatchState.MountEntry { Fingerprint = MountFingerprint, Result = LibraryFolderWatchState.MountResult.Watching }],
        };
        context.Library.Add(library);

        var joe = new AppUserBuilder("joe", "joe@example.com").Build();
        joe.Id = 1;
        joe.NormalizedUserName = "JOE";
        joe.ApiKey = JoeApiKey;
        joe.Libraries.Add(library);
        joe.Devices.Add(new Device { Name = "Joe's Kindle", IpAddress = JoeIp, EmailAddress = KindleEmail });
        context.AppUser.Add(joe);
        context.ClientDevice.Add(new ClientDevice
        {
            AppUser = joe,
            FriendlyName = "Desktop",
            CurrentClientInfo = new ClientInfoData
            {
                IpAddress = JoeIp,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                Browser = "Firefox",
            },
        });

        var amelia = new AppUserBuilder("amelia", "amelia@example.com").Build();
        amelia.Id = 2;
        amelia.NormalizedUserName = "AMELIA";
        amelia.ApiKey = null;
        amelia.ConfirmationToken = string.Empty;
        context.AppUser.Add(amelia);

        await context.SaveChangesAsync();

        var license = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.LicenseKey);
        license.Value = LicenseKey;
        var oidc = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.OidcConfiguration);
        oidc.Value = JsonSerializer.Serialize(new OidcConfigDto
        {
            Authority = "https://auth.example.com/realms/kavita",
            ClientId = "kavita-client",
            Secret = OidcSecret,
        });
        var emailHost = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.EmailHost);
        emailHost.Value = string.Empty;
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
        GC.SuppressFinalize(this);
    }
}
