using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Scanner;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DebugExport;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Kavita.Services.Builders;
using Kavita.Services.DebugExport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.DebugExport;

public class ExportSystemInfoServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper), IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), $"kavita_export_system_{Guid.NewGuid()}");
    private readonly IMountDetectorService _mountDetector = Substitute.For<IMountDetectorService>();
    private readonly ILicenseService _licenseService = Substitute.For<ILicenseService>();

    [Theory]
    [InlineData("//192.168.1.5/books", "cifs", "//192.0.2.1/books")]
    [InlineData("nas.home.lan:/export/books", "nfs", "host-1.invalid:/export/books")]
    [InlineData(@"C:\", "NTFS", @"C:\")]
    [InlineData("/dev/sda1", "ext4", "/dev/sda1")]
    [InlineData("overlay", "overlay", "overlay")]
    public async Task MountSourceHostIsReplaced(string source, string type, string expected)
    {
        _mountDetector.Detect(Arg.Any<string>()).Returns(new MountInfo("/data", source, type));

        var (info, result) = await Build();

        var mount = info.Libraries.Single().Folders.Single().Mount!;
        Assert.Equal(expected, mount.Source);
        Assert.Equal(expected, info.Config.Mount!.Source);
        if (source.Contains("192.168.1.5"))
        {
            Assert.Equal("192.0.2.1", result.Junk.Replaced["192.168.1.5"]);
        }
    }

    [Fact]
    public async Task MountFingerprintMatchesTheScrubbedWatchState()
    {
        var mount = new MountInfo("/data", "//192.168.1.5/books", "cifs");
        _mountDetector.Detect(Arg.Any<string>()).Returns(mount);

        var (info, result) = await Build();

        var fingerprint = info.Libraries.Single().Folders.Single().Mount!.Fingerprint;
        Assert.NotEqual(mount.Fingerprint, fingerprint);
        Assert.Equal(result.Junk.Replace(ExportScrubKind.Fingerprint, mount.Fingerprint), fingerprint);
        Assert.Null(info.Config.Mount!.Fingerprint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableLicenseIsRecordedNotFatal(bool throws)
    {
        if (throws)
        {
            _licenseService.GetLicenseInfo(false, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("cache unavailable"));
        }
        else
        {
            _licenseService.GetLicenseInfo(false, Arg.Any<CancellationToken>()).Returns((LicenseInfoDto?)null);
        }

        var (info, _) = await Build(licenseKey: "license-key-abcdef0123456789");

        Assert.True(info.KavitaPlus.HasLicense);
        Assert.Equal(ExportSystemInfo.Unavailable, info.KavitaPlus.License);
    }

    [Fact]
    public async Task MountThatThrowsIsLeftOutNotFatal()
    {
        _mountDetector.Detect(Arg.Any<string>()).Throws(new IOException("The device is not ready"));

        var (info, _) = await Build();

        Assert.Null(info.Libraries.Single().Folders.Single().Mount);
        Assert.Null(info.Config.Mount);
    }

    [Fact]
    public async Task LicenseEmailAndDiscordNeverShip()
    {
        _licenseService.GetLicenseInfo(false, Arg.Any<CancellationToken>()).Returns(new LicenseInfoDto
        {
            State = KavitaPlusSubscriptionState.Active,
            IsActive = true,
            RegisteredEmail = "joe@example.com",
            DiscordId = "498217340912",
            DiscordUsername = "joe_reads",
            InstallId = "install-1",
        });

        var (info, _) = await Build(licenseKey: "license-key-abcdef0123456789");

        var json = info.ToJson();
        Assert.Contains("\"Active\"", json);
        Assert.DoesNotContain("joe@example.com", json);
        Assert.DoesNotContain("498217340912", json);
        Assert.DoesNotContain("joe_reads", json);
    }

    [Fact]
    public async Task ReadTimesReportBothReads()
    {
        Directory.CreateDirectory(_directory);
        var first = Path.Join(_directory, "a.cbz");
        var second = Path.Join(_directory, "b.cbz");
        await File.WriteAllBytesAsync(first, new byte[4096]);
        await File.WriteAllBytesAsync(second, new byte[1024]);

        var (info, _) = await Build(files: [first, second, Path.Join(_directory, "missing.cbz")]);

        var rows = info.Libraries.Single().ReadTimes;
        Assert.Equal(3, rows.Count);
        Assert.Equal([1024L, 4096L], rows.Where(r => r.Error == null).Select(r => r.Bytes!.Value).Order());
        Assert.All(rows.Where(r => r.Error == null), r =>
        {
            Assert.NotNull(r.FirstMs);
            Assert.NotNull(r.SecondMs);
        });
        Assert.Equal(nameof(FileNotFoundException), rows.Single(r => r.Bytes == null).Error);
    }

    [Fact]
    public async Task WriteTimesLeaveNoFile()
    {
        var (info, _) = await Build();

        var writeTimes = info.Config.WriteTimes;
        Assert.Null(writeTimes.Error);
        Assert.Equal(20, writeTimes.Rounds);
        Assert.NotNull(writeTimes.MedianMs);
        Assert.Empty(Directory.EnumerateFileSystemEntries(ExportFolder));
    }

    private string ExportFolder => Path.Join(_directory, "export");

    private async Task<(ExportSystemInfo Info, ExportScrubResult Result)> Build(string? licenseKey = null, IEnumerable<string>? files = null)
    {
        var (_, context, _) = await CreateDatabase();
        await Seed(context, licenseKey, files ?? []);
        Directory.CreateDirectory(ExportFolder);

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(new FileSystem());
        directoryService.ConfigDirectory.Returns(_directory);

        var result = new ExportScrubResult(new ExportJunk(), new Dictionary<string, string>(), "kavita-debug", "password", "auth-key");
        var service = new ExportSystemInfoService(Substitute.For<ILogger<ExportSystemInfoService>>(), context, directoryService, _mountDetector, _licenseService);

        return (await service.BuildAsync(result, ExportFolder), result);
    }

    private static async Task Seed(DataContext context, string? licenseKey, IEnumerable<string> files)
    {
        if (licenseKey != null)
        {
            var setting = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.LicenseKey);
            setting.Value = licenseKey;
        }

        var chapter = new ChapterBuilder("1").Build();
        foreach (var file in files)
        {
            chapter.Files.Add(new MangaFileBuilder(file, MangaFormat.Archive).Build());
        }

        var volume = new VolumeBuilder("1").Build();
        volume.Chapters.Add(chapter);
        var series = new SeriesBuilder("Test").Build();
        series.Volumes.Add(volume);
        series.LibraryId = 1;
        context.Series.Add(series);

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
