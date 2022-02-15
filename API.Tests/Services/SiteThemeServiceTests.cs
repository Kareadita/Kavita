using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Theme;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class SiteThemeServiceTests
{
    private readonly ILogger<SiteThemeService> _logger = Substitute.For<ILogger<SiteThemeService>>();
    private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();

    private readonly DbConnection _connection;
    private readonly DataContext _context;
    private readonly IUnitOfWork _unitOfWork;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string BookmarkDirectory = "C:/kavita/config/bookmarks/";
    private const string SiteThemeDirectory = "C:/kavita/config/themes/";

    public SiteThemeServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, mapper, null);
    }

    #region Setup

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

            _context.ServerSetting.Update(setting);

            _context.AppUser.Add(new AppUser()
            {
                UserName = "Joe",
                UserPreferences = new AppUserPreferences
                {
                    Theme = Seed.DefaultThemes[1]
                }
            });

            _context.Library.Add(new Library()
            {
                Name = "Manga",
                Folders = new List<FolderPath>()
                {
                    new FolderPath()
                    {
                        Path = "C:/data/"
                    }
                }
            });
            return await _context.SaveChangesAsync() > 0;
        }

        private static MockFileSystem CreateFileSystem()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory("C:/kavita/");
            fileSystem.AddDirectory("C:/kavita/config/");
            fileSystem.AddDirectory(CacheDirectory);
            fileSystem.AddDirectory(CoverImageDirectory);
            fileSystem.AddDirectory(BackupDirectory);
            fileSystem.AddDirectory(BookmarkDirectory);
            fileSystem.AddDirectory(SiteThemeDirectory);
            fileSystem.AddDirectory("C:/data/");

            return fileSystem;
        }

    #endregion

    [Fact]
    public async Task Scan_ShouldFindCustomFile()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new SiteThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));
    }

    [Fact]
    public async Task Scan_ShouldOnlyInsertOnceOnSecondScan()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new SiteThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));

        await siteThemeService.Scan();

        Assert.Single((await _unitOfWork.SiteThemeRepository.GetThemeDtos()).Where(t => t.Name.ToLower().Equals("custom")));
    }

    [Fact]
    public async Task GetContent_ShouldReturnContent()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new SiteThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            Provider = ThemeProvider.User,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var content = await siteThemeService.GetContent((await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("Custom")).Id);
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.Equal("123", content);
    }


}
