using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.DebugExport;
using Kavita.Models.Entities.Enums;
using Kavita.Services.DebugExport;
using NSubstitute;
using Xunit;

namespace Kavita.Services.Tests.DebugExport;

public class ExportLogServiceTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), $"kavita_export_logs_{Guid.NewGuid()}");

    public ExportLogServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task OnlyTheChosenFiveDaysAreWritten()
    {
        string[] days = ["20261002", "20261003", "20261004", "20261005", "20261006", "20261007", "20261008", "20261029"];
        var files = days.Select(day => WriteLog($"kavita{day}.log", $"[joe] read on {day}")).ToList();

        var entries = await Export(files);

        Assert.Equal(["logs/kavita20261005.log", "logs/kavita20261006.log", "logs/kavita20261007.log", "logs/kavita20261008.log", "logs/kavita20261029.log"],
            entries.Keys.Order());
        Assert.Equal("[user-1] read on 20261029", entries["logs/kavita20261029.log"].Trim());
    }

    [Fact]
    public async Task TodaysLogIsReadWhileSerilogHoldsIt()
    {
        var today = WriteLog("kavita20261030.log", "[joe] first line\n[joe] second line");
        await using var serilog = new FileStream(today, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        var entries = await Export([today]);

        Assert.Equal(["[user-1] first line", "[user-1] second line"], entries["logs/kavita20261030.log"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')));
    }

    private string WriteLog(string name, string content)
    {
        var path = Path.Join(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<Dictionary<string, string>> Export(IEnumerable<string> logFiles)
    {
        var backupService = Substitute.For<IBackupService>();
        backupService.GetLogFiles().Returns(logFiles);
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(new FileSystem());

        var junk = new ExportJunk();
        var usernames = new Dictionary<string, string> { ["joe"] = junk.Replace(ExportScrubKind.Username, "joe", "user-1")! };

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            await new ExportLogService(backupService, directoryService).WriteLogsAsync(zip, new ExportTextScrubber(junk, usernames));
        }

        stream.Position = 0;
        using var read = new ZipArchive(stream, ZipArchiveMode.Read);
        return read.Entries.ToDictionary(e => e.FullName, e => new StreamReader(e.Open()).ReadToEnd());
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
