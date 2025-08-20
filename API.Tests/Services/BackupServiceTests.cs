using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class BackupServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ILogger<BackupService> _logger = Substitute.For<ILogger<BackupService>>();
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();


    #region GetLogFiles

    [Fact]
    public async Task GetLogFiles_ExpectAllFiles_NoRollingFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
        filesystem.AddFile($"{LogDirectory}kavita1.log", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, unitOfWork, ds, _messageHub);

        var backupLogFiles = backupService.GetLogFiles(false).ToList();
        Assert.Single(backupLogFiles);
        Assert.Equal(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath($"{LogDirectory}kavita.log"), API.Services.Tasks.Scanner.Parser.Parser.NormalizePath(backupLogFiles.First()));
    }

    [Fact]
    public async Task GetLogFiles_ExpectAllFiles_WithRollingFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
        filesystem.AddFile($"{LogDirectory}kavita20200213.log", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, unitOfWork, ds, _messageHub);

        var backupLogFiles = backupService.GetLogFiles().Select(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath).ToList();
        Assert.Contains(backupLogFiles, file => file.Equals(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath($"{LogDirectory}kavita.log")) || file.Equals(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath($"{LogDirectory}kavita1.log")));
    }


    #endregion

    #region BackupFiles

    // I don't think I can unit test this due to ZipFile.Create
    // [Fact]
    // public async Task BackupDatabase_ExpectAllFiles()
    // {
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
    //     filesystem.AddFile($"{ConfigDirectory}kavita.db", new MockFileData(""));
    //     filesystem.AddFile($"{CoverImageDirectory}1.png", new MockFileData(""));
    //     filesystem.AddFile($"{BookmarkDirectory}1.png", new MockFileData(""));
    //     filesystem.AddFile($"{ConfigDirectory}appsettings.json", new MockFileData(""));
    //     filesystem.AddFile($"{ThemesDirectory}joe.css", new MockFileData(""));
    //
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var inMemorySettings = new Dictionary<string, string> {
    //         {"Logging:File:Path", $"{LogDirectory}kavita.log"},
    //         {"Logging:File:MaxRollingFiles", "0"},
    //     };
    //     IConfiguration configuration = new ConfigurationBuilder()
    //         .AddInMemoryCollection(inMemorySettings)
    //         .Build();
    //
    //     var backupService = new BackupService(_logger, _unitOfWork, ds, configuration, _messageHub);
    //
    //     await backupService.BackupDatabase();
    //
    //
    //     var files = ds.GetFiles(BackupDirectory).ToList();
    //     Assert.NotEmpty(files);
    //     var zipFile = files.FirstOrDefault();
    //     Assert.NotNull(zipFile);
    //     using var zipArchive = ZipFile.OpenRead(zipFile);
    //
    // }

    #endregion
}
