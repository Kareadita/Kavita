using System;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Data;
using API.Data.AutoMapper;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using AutoMapper;
using Hangfire;
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

    #region BackupDatabaseFile

    [Fact]
    public async Task BackupDatabaseFile_WithValidPath_CreatesBackup()
    {
        // Arrange - Create a file-based SQLite database for testing VACUUM INTO
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kavita_test_{Guid.NewGuid()}.db");
        var tempBackupDir = Path.Combine(Path.GetTempPath(), $"kavita_backup_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempBackupDir);

        try
        {
            var connectionString = $"Data Source={tempDbPath}";
            var contextOptions = new DbContextOptionsBuilder<DataContext>()
                .UseSqlite(connectionString)
                .EnableSensitiveDataLogging()
                .Options;

            await using var context = new DataContext(contextOptions);
            await context.Database.EnsureCreatedAsync();

            var filesystem = CreateFileSystem();
            var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);

            var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
            var mapper = config.CreateMapper();

            GlobalConfiguration.Configuration.UseInMemoryStorage();
            var unitOfWork = new UnitOfWork(context, mapper, null);

            var backupService = new BackupService(_logger, unitOfWork, ds, _messageHub);

            // Act - Use reflection to call the private method
            var methodInfo = typeof(BackupService).GetMethod("BackupDatabaseFile",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(methodInfo);

            var task = (Task?)methodInfo.Invoke(backupService, new object[] { tempBackupDir });
            Assert.NotNull(task);
            await task;

            // Assert
            var backupPath = Path.Combine(tempBackupDir, "kavita.db");
            Assert.True(File.Exists(backupPath), "Backup file should be created");

            // Verify the backup is a valid SQLite database
            var backupConnectionString = $"Data Source={backupPath}";
            var backupContextOptions = new DbContextOptionsBuilder<DataContext>()
                .UseSqlite(backupConnectionString)
                .Options;

            await using var backupContext = new DataContext(backupContextOptions);
            // If we can create the context without error, the backup is valid
            Assert.True(await backupContext.Database.CanConnectAsync());
        }
        finally
        {
            try
            {
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }

            if (Directory.Exists(tempBackupDir))
            {
                Directory.Delete(tempBackupDir, true);
            }
            }
            catch (Exception)
            {
                // Ignore cleanup exceptions
            }
        }
    }

    [Fact]
    public async Task BackupDatabaseFile_WithPathContainingSingleQuote_ThrowsArgumentException()
    {
        // Arrange
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, unitOfWork, ds, _messageHub);

        // Act - Use reflection to call the private method with a path containing single quote
        var methodInfo = typeof(BackupService).GetMethod("BackupDatabaseFile",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(methodInfo);

        var invalidPath = "/tmp/test'injection";

        // Assert - The ArgumentException is thrown directly since the validation happens before async work
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var task = (Task?)methodInfo.Invoke(backupService, new object[] { invalidPath });
            Assert.NotNull(task);
            await task;
        });

        Assert.Contains("invalid characters", exception.Message);
    }

    #endregion

    #region BackupService Initialization

    [Fact]
    public async Task BackupService_BackupFilesList_DoesNotContainDatabaseFiles()
    {
        // Arrange
        var (unitOfWork, context, _) = await CreateDatabase();
        var filesystem = CreateFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);

        // Act
        var backupService = new BackupService(_logger, unitOfWork, ds, _messageHub);

        // Assert - Use reflection to access the private _backupFiles field
        var backupFilesField = typeof(BackupService).GetField("_backupFiles",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(backupFilesField);

        var backupFiles = backupFilesField.GetValue(backupService) as System.Collections.Generic.IList<string>;
        Assert.NotNull(backupFiles);

        // Verify that database files are NOT in the backup list (since we now use VACUUM INTO)
        Assert.DoesNotContain("kavita.db", backupFiles);
        Assert.DoesNotContain("kavita.db-shm", backupFiles);
        Assert.DoesNotContain("kavita.db-wal", backupFiles);

        // Verify appsettings.json is still in the list
        Assert.Contains("appsettings.json", backupFiles);
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
