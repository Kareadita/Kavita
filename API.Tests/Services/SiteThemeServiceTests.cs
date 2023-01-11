using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;


public abstract class SiteThemeServiceTest
{
    protected readonly ITestOutputHelper _testOutputHelper;
    protected readonly IEventHub _messageHub = Substitute.For<IEventHub>();

    protected readonly DbConnection _connection;
    protected readonly DataContext _context;
    protected readonly IUnitOfWork _unitOfWork;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string BookmarkDirectory = "C:/kavita/config/bookmarks/";
    protected const string SiteThemeDirectory = "C:/kavita/config/themes/";

    protected SiteThemeServiceTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
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
        await Seed.SeedThemes(_context);

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
                Theme = Seed.DefaultThemes[0]
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
        fileSystem.AddDirectory("C:/data/");

        return fileSystem;
    }

    protected async Task ResetDb()
    {
        _context.SiteTheme.RemoveRange(_context.SiteTheme);
        await _context.SaveChangesAsync();
        // Recreate defaults
        await Seed.SeedThemes(_context);
    }

    #endregion
}



[Collection("UpdateDefault_ShouldThrowOnInvalidId")]
public class SiteThemeServiceTest1 : SiteThemeServiceTest
{
    public SiteThemeServiceTest1(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task UpdateDefault_ShouldThrowOnInvalidId()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[UpdateDefault_ShouldThrowOnInvalidId] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.User,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KavitaException>(async () => await siteThemeService.UpdateDefault(10));
        Assert.Equal("Theme file missing or invalid", ex.Message);

    }

}

[Collection("Scan_ShouldFindCustomFile")]
public class SiteThemeServiceTest2 : SiteThemeServiceTest
{
    public SiteThemeServiceTest2(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task Scan_ShouldFindCustomFile()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[Scan_ShouldOnlyInsertOnceOnSecondScan] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));
    }
}

[Collection("Scan_ShouldOnlyInsertOnceOnSecondScan")]
public class SiteThemeServiceTest3 : SiteThemeServiceTest
{
    public SiteThemeServiceTest3(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task Scan_ShouldOnlyInsertOnceOnSecondScan()
    {
        await ResetDb();
        _testOutputHelper.WriteLine(
            $"[Scan_ShouldOnlyInsertOnceOnSecondScan] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));

        await siteThemeService.Scan();

        var customThemes = (await _unitOfWork.SiteThemeRepository.GetThemeDtos()).Where(t =>
            t.Name.ToNormalized().Equals("custom".ToNormalized()));

        Assert.Single(customThemes);
    }
}

[Collection("Scan_ShouldDeleteWhenFileDoesntExistOnSecondScan")]
public class SiteThemeServiceTest4 : SiteThemeServiceTest
{
    public SiteThemeServiceTest4(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task Scan_ShouldDeleteWhenFileDoesntExistOnSecondScan()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[Scan_ShouldDeleteWhenFileDoesntExistOnSecondScan] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));

        filesystem.RemoveFile($"{SiteThemeDirectory}custom.css");
        await siteThemeService.Scan();

        var themes = (await _unitOfWork.SiteThemeRepository.GetThemeDtos());

        Assert.Equal(0, themes.Count(t =>
            t.Name.ToNormalized().Equals("custom".ToNormalized())));
    }
}

[Collection("GetContent_ShouldReturnContent")]
public class SiteThemeServiceTest5 : SiteThemeServiceTest
{
    public SiteThemeServiceTest5(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task GetContent_ShouldReturnContent()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[GetContent_ShouldReturnContent] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
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

[Collection("UpdateDefault_ShouldHaveOneDefault")]
public class SiteThemeServiceTest6 : SiteThemeServiceTest
{
    public SiteThemeServiceTest6(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task UpdateDefault_ShouldHaveOneDefault()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[UpdateDefault_ShouldHaveOneDefault] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.User,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var customTheme = (await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("Custom"));

        await siteThemeService.UpdateDefault(customTheme.Id);



        Assert.Equal(customTheme.Id, (await _unitOfWork.SiteThemeRepository.GetDefaultTheme()).Id);
    }
}



