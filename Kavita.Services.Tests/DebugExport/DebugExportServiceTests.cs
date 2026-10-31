using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DebugExport;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Models.Entities.Enums;
using Kavita.Services.DebugExport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Kavita.Services.Tests.DebugExport;

public class DebugExportServiceTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), $"kavita_debug_export_{Guid.NewGuid()}");
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly IExportDatabaseScrubService _scrubService = Substitute.For<IExportDatabaseScrubService>();
    private readonly IExportSystemInfoService _systemInfoService = Substitute.For<IExportSystemInfoService>();
    private readonly IExportLogService _logService = Substitute.For<IExportLogService>();
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();

    public DebugExportServiceTests()
    {
        Directory.CreateDirectory(_directory);

        _backupService.SnapshotDatabase(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => File.WriteAllTextAsync(ci.ArgAt<string>(0), "database"));
        _scrubService.ScrubAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExportScrubResult(new ExportJunk(), new Dictionary<string, string>(), "kavita-debug", "password", "auth-key"));
        _systemInfoService.BuildAsync(Arg.Any<ExportScrubResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SystemInfo());
        _logService.WriteLogsAsync(Arg.Any<ZipArchive>(), Arg.Any<ExportTextScrubber>(), Arg.Any<CancellationToken>())
            .Returns(ci => WriteLogAsync(ci.ArgAt<ZipArchive>(0), "started scan"));
    }

    [Fact]
    public async Task ZipHoldsDatabaseLogsAndSystemInfo()
    {
        await CreateService().ExportAsync();

        var folder = Directory.EnumerateDirectories(_directory).Single();
        var zipPath = Path.Join(folder, DebugExportService.ZipName);
        Assert.Equal([zipPath], Directory.EnumerateFileSystemEntries(folder));

        await using (var zip = await ZipFile.OpenReadAsync(zipPath))
        {
            Assert.Equal(["kavita.db", "logs/kavita20261031.log", "system-info.json"], zip.Entries.Select(e => e.FullName));
        }

        var exportId = Path.GetFileName(folder);
        await _eventHub.Received(1).SendMessageAsync(MessageFactory.Info,
            Arg.Is<SignalRMessageDto>(m => m.Code == MessageEventCode.DebugExportReady &&
                                           ((DebugExportReadyEventBodyDto) m.Body!).ExportId == exportId &&
                                           ((DebugExportReadyEventBodyDto) m.Body!).Bytes == new FileInfo(zipPath).Length),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogScrubberKnowsSystemInfoHosts()
    {
        _systemInfoService.BuildAsync(Arg.Any<ExportScrubResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                ci.ArgAt<ExportScrubResult>(0).Junk.Replace(ExportScrubKind.Host, "nas.home.lan");
                return SystemInfo();
            });
        ExportTextScrubber? scrubber = null;
        _logService.WriteLogsAsync(Arg.Any<ZipArchive>(), Arg.Do<ExportTextScrubber>(s => scrubber = s), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await CreateService().ExportAsync();

        Assert.Equal("mounted host-1.invalid", scrubber!.Scrub("mounted nas.home.lan"));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("scrub")]
    [InlineData("logs")]
    public async Task FailedExportSendsFailedAndLeavesNothing(string failingStep)
    {
        var error = new IOException("There is not enough space on the disk");
        switch (failingStep)
        {
            case "snapshot":
                _backupService.SnapshotDatabase(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
                break;
            case "scrub":
                _scrubService.ScrubAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
                break;
            case "logs":
                _logService.WriteLogsAsync(Arg.Any<ZipArchive>(), Arg.Any<ExportTextScrubber>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
                break;
        }

        await CreateService().ExportAsync();

        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
        await _eventHub.Received(1).SendMessageAsync(MessageFactory.Error,
            Arg.Is<SignalRMessageDto>(m => m.Code == MessageEventCode.DebugExportFailed),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _eventHub.DidNotReceive().SendMessageAsync(MessageFactory.Info,
            Arg.Is<SignalRMessageDto>(m => m.Code == MessageEventCode.DebugExportReady),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("20261031-0102PM-a3f9", DebugExportService.ZipName, true)]
    [InlineData("20261031-0102PM-a3f9", DebugExportService.ZipName + ".partial", false)]
    [InlineData("20261031-0102PM-A3F9", DebugExportService.ZipName, false)]
    [InlineData(@"..\..\config", DebugExportService.ZipName, false)]
    [InlineData("20261031-0102PM-a3f9\n", DebugExportService.ZipName, false)]
    [InlineData("", DebugExportService.ZipName, false)]
    public void ZipPathOnlyForAFinishedExport(string exportId, string fileOnDisk, bool found)
    {
        var folder = Directory.CreateDirectory(Path.Join(_directory, "20261031-0102PM-a3f9"));
        File.WriteAllText(Path.Join(folder.FullName, fileOnDisk), "zip");

        var path = CreateService().GetZipPath(exportId);

        Assert.Equal(found ? Path.Join(folder.FullName, DebugExportService.ZipName) : null, path);
    }

    private DebugExportService CreateService()
    {
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(new FileSystem());
        directoryService.DebugExportDirectory.Returns(_directory);

        return new DebugExportService(Substitute.For<ILogger<DebugExportService>>(), directoryService, _backupService,
            _scrubService, _systemInfoService, _logService, _eventHub);
    }

    private static async Task WriteLogAsync(ZipArchive zip, string line)
    {
        await using var writer = new StreamWriter(await zip.CreateEntry("logs/kavita20261031.log").OpenAsync());
        await writer.WriteLineAsync(line);
    }

    private static ExportSystemInfo SystemInfo() => new(
        new ExportSystemInfo.ExportSection(DateTime.UtcNow, "0.9.1.16", "kavita-debug", "password", "auth-key"),
        new ExportSystemInfo.RuntimeSection("Microsoft Windows", "X64", ".NET 10.0.0", "win-x64", false, 8, "English (United States)"),
        new ExportSystemInfo.ConfigSection("C:/kavita/config", null, new ExportSystemInfo.WriteTimesSection(20, 4096, 0.9, 4.8, null)),
        [],
        new ExportSystemInfo.KavitaPlusSection(false, null, "token"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }

        GC.SuppressFinalize(this);
    }
}
