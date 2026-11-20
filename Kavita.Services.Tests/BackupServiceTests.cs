using System.Collections;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Reflection;
using AutoMapper;
using Hangfire;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Extensions;
using Kavita.Database.Tests;
using Kavita.Models.AutoMapper;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Models.Sqlite;
using Kavita.Services.Images;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class BackupServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ILogger<BackupService> _logger = Substitute.For<ILogger<BackupService>>();
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();
    private readonly IImageFileService _imageFileService = Substitute.For<IImageFileService>();


    #region GetLogFiles

    [Fact]
    public async Task GetLogFiles_ExpectAllFiles_NoRollingFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
        filesystem.AddFile($"{LogDirectory}kavita1.log", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, unitOfWork, ds, _imageFileService, _messageHub);

        var backupLogFiles = backupService.GetLogFiles(false).ToList();
        Assert.Single((IEnumerable)backupLogFiles);
        Assert.Equal(Parser.NormalizePath($"{LogDirectory}kavita.log"), Parser.NormalizePath(backupLogFiles.First()));
    }

    [Fact]
    public async Task GetLogFiles_ExpectAllFiles_WithRollingFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
        filesystem.AddFile($"{LogDirectory}kavita20200213.log", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, unitOfWork, ds, _imageFileService, _messageHub);

        var backupLogFiles = backupService.GetLogFiles().Select(Parser.NormalizePath).ToList();
        Assert.Contains(backupLogFiles, file => file.Equals(Parser.NormalizePath($"{LogDirectory}kavita.log")) || file.Equals(Parser.NormalizePath($"{LogDirectory}kavita1.log")));
    }


    #endregion

    #region SnapshotDatabase

    [Fact]
    public async Task SnapshotDatabase_WithValidPath_CreatesBackup()
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
            var unitOfWork = new UnitOfWork(context, mapper, null!);

            var backupService = new BackupService(_logger, unitOfWork, ds, _imageFileService, _messageHub);

            var backupPath = Path.Combine(tempBackupDir, "kavita.db");
            await backupService.SnapshotDatabase(backupPath);

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

    [Theory]
    [InlineData(DbJournalMode.Wal)]
    [InlineData(DbJournalMode.Delete)]
    public async Task SnapshotDatabase_KeepsJournalMode(DbJournalMode journalMode)
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kavita_test_{Guid.NewGuid()}.db");
        var tempBackupDir = Path.Combine(Path.GetTempPath(), $"kavita_backup_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempBackupDir);
        var backupPath = Path.Combine(tempBackupDir, "kavita.db");

        try
        {
            var contextOptions = new DbContextOptionsBuilder<DataContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = tempDbPath, Pooling = false }.ToString())
                .Options;

            await using (var context = new DataContext(contextOptions))
            {
                await context.Database.EnsureCreatedAsync();
                var pragma = "PRAGMA journal_mode=" + journalMode + ";";
                await context.Database.ExecuteSqlRawAsync(pragma);

                var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), CreateFileSystem());
                var unitOfWork = new UnitOfWork(context, new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>()).CreateMapper(), null!);
                await new BackupService(_logger, unitOfWork, ds, _imageFileService, _messageHub).SnapshotDatabase(backupPath);
            }

            Assert.Equal(journalMode, await ReadJournalMode(backupPath));
            Assert.False(File.Exists(backupPath + "-wal"));
        }
        finally
        {
            try
            {
                File.Delete(tempDbPath);
                Directory.Delete(tempBackupDir, true);
            }
            catch (Exception)
            {
                // Ignore cleanup exceptions
            }
        }
    }

    private static async Task<DbJournalMode> ReadJournalMode(string databaseFile)
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databaseFile, Pooling = false }.ToString())
            .Options;
        await using var context = new DataContext(options);
        return await context.Database.GetJournalModeAsync();
    }

    [Fact]
    public async Task SnapshotDatabase_PathWithQuote_CreatesCopy()
    {
        var tempBackupDir = Path.Combine(Path.GetTempPath(), $"kavita_backup_test_{Guid.NewGuid()}", "o'brien's library");
        Directory.CreateDirectory(tempBackupDir);
        var backupPath = Path.Combine(tempBackupDir, "kavita.db");

        try
        {
            var (unitOfWork, _, _) = await CreateDatabase();
            var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), CreateFileSystem());

            await new BackupService(_logger, unitOfWork, ds, _imageFileService, _messageHub).SnapshotDatabase(backupPath);

            Assert.True(File.Exists(backupPath));
        }
        finally
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(tempBackupDir)!, true);
            }
            catch (Exception)
            {
                // Ignore cleanup exceptions
            }
        }
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
        var backupService = new BackupService(_logger, unitOfWork, ds, _imageFileService, _messageHub);

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

    #region CopyCoverImages

    [Fact]
    public async Task Backup_CopiesByOrigin()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var filesystem = CreateFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var imageFileService = new ImageFileService(unitOfWork, Substitute.For<IImagePipeline>(), ds, _messageHub,
            Substitute.For<ILogger<ImageFileService>>());

        var rows = new[]
        {
            new ImageFile { Kind = ImageKind.PersonCover, FileName = "person1.webp", Origin = ImageOrigin.Upload },
            new ImageFile { Kind = ImageKind.UserCover, FileName = "user1.webp", Origin = ImageOrigin.Oidc },
            new ImageFile { Kind = ImageKind.Publisher, FileName = "viz.png", Origin = ImageOrigin.CoversDb, Key = "Viz" },
            new ImageFile { Kind = ImageKind.ChapterCover, FileName = "v1_c1.webp", Origin = ImageOrigin.Scan },
            new ImageFile { Kind = ImageKind.SeriesCover, FileName = "series1.webp", Origin = ImageOrigin.KavitaPlus },
            new ImageFile { Kind = ImageKind.Bookmark, FileName = "1/1/1/1.webp", Origin = ImageOrigin.Legacy },
        };
        context.ImageFile.AddRange(rows);
        await context.SaveChangesAsync();

        foreach (var row in rows.Where(r => r.Kind != ImageKind.Bookmark))
        {
            filesystem.AddFile(filesystem.Path.Join(await imageFileService.GetImageDirectoryAsync(row.Kind), row.FileName), new MockFileData([1]));
        }
        filesystem.AddFile(BookmarkDirectory + "1/1/1/1.webp", new MockFileData([1]));

        var backupService = new BackupService(_logger, unitOfWork, ds, imageFileService, _messageHub);
        var temp = TempDirectory + "backup/";

        await backupService.CopyCoverImagesToBackupDirectory(temp);

        Assert.True(filesystem.File.Exists(temp + "covers/person1.webp"));
        Assert.True(filesystem.File.Exists(temp + "covers/user1.webp"));
        Assert.True(filesystem.File.Exists(temp + "images/publishers/viz.png"));
        Assert.False(filesystem.File.Exists(temp + "covers/v1_c1.webp"));
        Assert.False(filesystem.File.Exists(temp + "covers/series1.webp"));
        Assert.False(filesystem.File.Exists(temp + "covers/1.webp"));
    }

    #endregion

    #region BackupFiles

    [Fact]
    public async Task BackupDatabase_ZipHoldsEverythingBackedUp()
    {
        var root = Path.Join(Path.GetTempPath(), "kavita-backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (unitOfWork, context, _) = await CreateDatabase();
            var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new RootedFileSystem(root));
            var imageFileService = new ImageFileService(unitOfWork, Substitute.For<IImagePipeline>(), ds, _messageHub,
                Substitute.For<ILogger<ImageFileService>>());

            var backups = Path.Join(root, "config", "backups");
            var bookmarks = Path.Join(root, "config", "bookmarks");
            (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.BackupDirectory)).Value = backups;
            (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.BookmarkDirectory)).Value = bookmarks;
            context.ImageFile.AddRange(
                new ImageFile { Kind = ImageKind.PersonCover, FileName = "person1.webp", Origin = ImageOrigin.Upload },
                new ImageFile { Kind = ImageKind.ChapterCover, FileName = "v1_c1.webp", Origin = ImageOrigin.Scan });
            await context.SaveChangesAsync();

            WriteFile(ds.ConfigDirectory, "appsettings.json");
            WriteFile(ds.ConfigDirectory, "progress_export.csv");
            WriteFile(ds.LogDirectory, "kavita.log");
            WriteFile(ds.LogDirectory, "kavita20261119.log");
            WriteFile(ds.CoverImageDirectory, "person1.webp");
            WriteFile(ds.CoverImageDirectory, "v1_c1.webp");
            WriteFile(ds.TemplateDirectory, "EmailConfirm.html");
            WriteFile(ds.EpubFontDirectory, "Fira.woff2");
            WriteFile(ds.SiteThemeDirectory, "joe.css");
            WriteFile(Path.Join(bookmarks, "1", "10", "100"), "001.webp");

            var backupService = new BackupService(_logger, unitOfWork, ds, imageFileService, _messageHub);

            Assert.True(await backupService.BackupDatabase());

            var zip = Assert.Single(Directory.GetFiles(backups, "*.zip"));
            using var archive = ZipFile.OpenRead(zip);
            var entries = archive.Entries.Select(e => e.FullName).ToList();
            Assert.Equivalent(new[]
            {
                "kavita.db",
                "appsettings.json",
                "progress_export.csv",
                "logs/kavita.log",
                "logs/kavita20261119.log",
                "covers/person1.webp",
                "templates/EmailConfirm.html",
                "fonts/Fira.woff2",
                "themes/joe.css",
                "bookmarks/1/10/100/001.webp",
            }, entries.Where(e => !e.EndsWith('/')).ToList());
            Assert.False(Directory.Exists(Path.Join(backups, ".staging")));
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // A file still held open must not fail the test
            }
        }
    }

    private static void WriteFile(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Join(directory, fileName), [1, 2, 3]);
    }

    #endregion
}
