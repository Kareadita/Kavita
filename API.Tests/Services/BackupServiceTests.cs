using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
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

namespace API.Tests.Services;

public class BackupServiceTests
{
    private readonly ILogger<BackupService> _logger = Substitute.For<ILogger<BackupService>>();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();
    private readonly IConfiguration _config;

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string LogDirectory = "C:/kavita/config/logs/";
    private const string ConfigDirectory = "C:/kavita/config/";
    private const string BookmarkDirectory = "C:/kavita/config/bookmarks";
    private const string ThemesDirectory = "C:/kavita/config/theme";

    public BackupServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        _unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);
        _config = Substitute.For<IConfiguration>();

    }

    #region Setup

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<bool> SeedDb()
    {
        await _context.Database.MigrateAsync();
        var filesystem = CreateFileSystem();

        await Seed.SeedSettings(_context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        _context.ServerSetting.Update(setting);
        _context.Library.Add(new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder("C:/data/").Build())
            .Build());
        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDB()
    {
        _context.Series.RemoveRange(_context.Series.ToList());

        await _context.SaveChangesAsync();
    }

    private static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory("C:/kavita/");
        fileSystem.AddDirectory("C:/kavita/config/");
        fileSystem.AddDirectory(CacheDirectory);
        fileSystem.AddDirectory(CoverImageDirectory);
        fileSystem.AddDirectory(BackupDirectory);
        fileSystem.AddDirectory(LogDirectory);
        fileSystem.AddDirectory(ThemesDirectory);
        fileSystem.AddDirectory(BookmarkDirectory);
        fileSystem.AddDirectory("C:/data/");

        return fileSystem;
    }

    #endregion



    #region GetLogFiles

    [Fact]
    public void GetLogFiles_ExpectAllFiles_NoRollingFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
        filesystem.AddFile($"{LogDirectory}kavita1.log", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, _unitOfWork, ds, _messageHub);

        var backupLogFiles = backupService.GetLogFiles(false).ToList();
        Assert.Single(backupLogFiles);
        Assert.Equal(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath($"{LogDirectory}kavita.log"), API.Services.Tasks.Scanner.Parser.Parser.NormalizePath(backupLogFiles.First()));
    }

    [Fact]
    public void GetLogFiles_ExpectAllFiles_WithRollingFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{LogDirectory}kavita.log", new MockFileData(""));
        filesystem.AddFile($"{LogDirectory}kavita20200213.log", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var backupService = new BackupService(_logger, _unitOfWork, ds, _messageHub);

        var backupLogFiles = backupService.GetLogFiles().Select(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath).ToList();
        Assert.NotEmpty(backupLogFiles.Where(file => file.Equals(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath($"{LogDirectory}kavita.log")) || file.Equals(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath($"{LogDirectory}kavita1.log"))));
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
