using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Tests;

public abstract class AbstractDbTest
{
    protected readonly DbConnection _connection;
    protected readonly DataContext _context;
    protected readonly IUnitOfWork _unitOfWork;


    protected const string CacheDirectory = "C:/kavita/config/cache/";
    protected const string CoverImageDirectory = "C:/kavita/config/covers/";
    protected const string BackupDirectory = "C:/kavita/config/backups/";
    protected const string LogDirectory = "C:/kavita/config/logs/";
    protected const string BookmarkDirectory = "C:/kavita/config/bookmarks/";
    protected const string SiteThemeDirectory = "C:/kavita/config/themes/";
    protected const string TempDirectory = "C:/kavita/config/temp/";
    protected const string DataDirectory = "C:/data/";

    protected AbstractDbTest()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();

        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();


        _unitOfWork = new UnitOfWork(_context, mapper, null);
    }

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        return connection;
    }

    private async Task<bool> SeedDb()
    {
        await _context.Database.MigrateAsync();
        var filesystem = CreateFileSystem();

        await Seed.SeedSettings(_context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BookmarkDirectory).SingleAsync();
        setting.Value = BookmarkDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.TotalLogs).SingleAsync();
        setting.Value = "10";

        _context.ServerSetting.Update(setting);

        _context.Library.Add(new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder("C:/data/").Build())
            .Build());
        return await _context.SaveChangesAsync() > 0;
    }

    protected abstract Task ResetDb();

    protected static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory("C:/kavita/");
        fileSystem.AddDirectory("C:/kavita/config/");
        fileSystem.AddDirectory(CacheDirectory);
        fileSystem.AddDirectory(CoverImageDirectory);
        fileSystem.AddDirectory(BackupDirectory);
        fileSystem.AddDirectory(BookmarkDirectory);
        fileSystem.AddDirectory(SiteThemeDirectory);
        fileSystem.AddDirectory(LogDirectory);
        fileSystem.AddDirectory(TempDirectory);
        fileSystem.AddDirectory(DataDirectory);

        return fileSystem;
    }
}
