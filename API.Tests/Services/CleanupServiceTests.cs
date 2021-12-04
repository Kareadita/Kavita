using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
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

public class CleanupServiceTests
{
    private readonly ILogger<CleanupService> _logger = Substitute.For<ILogger<CleanupService>>();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";


    public CleanupServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        _unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);
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
        fileSystem.AddDirectory("C:/data/");

        return fileSystem;
    }

    #endregion


    #region DeleteSeriesCoverImages

    [Fact]
    public async Task DeleteSeriesCoverImages_ShouldDeleteAll()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}series_01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}series_03.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}series_1000.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDB();

        var s = DbFactory.Series("Test 1");
        s.CoverImage = "series_01.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);
        s = DbFactory.Series("Test 2");
        s.CoverImage = "series_03.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);
        s = DbFactory.Series("Test 3");
        s.CoverImage = "series_1000.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteSeriesCoverImages();

        Assert.Empty(ds.GetFiles(CoverImageDirectory));
    }

    [Fact]
    public async Task DeleteSeriesCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}series_01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}series_03.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}series_1000.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDB();

        // Add 2 series with cover images
        var s = DbFactory.Series("Test 1");
        s.CoverImage = "series_01.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);
        s = DbFactory.Series("Test 2");
        s.CoverImage = "series_03.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);


        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteSeriesCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region DeleteChapterCoverImages
    [Fact]
    public async Task DeleteChapterCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}v01_c01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}v01_c03.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}v01_c1000.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDB();

        // Add 2 series with cover images
        var s = DbFactory.Series("Test 1");
        var v = DbFactory.Volume("1");
        v.Chapters.Add(new Chapter()
        {
            CoverImage = "v01_c01.jpg"
        });
        v.CoverImage = "v01_c01.jpg";
        s.Volumes.Add(v);
        s.CoverImage = "series_01.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);

        s = DbFactory.Series("Test 2");
        v = DbFactory.Volume("1");
        v.Chapters.Add(new Chapter()
        {
            CoverImage = "v01_c03.jpg"
        });
        v.CoverImage = "v01_c03jpg";
        s.Volumes.Add(v);
        s.CoverImage = "series_03.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);


        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteChapterCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region DeleteTagCoverImages

    [Fact]
    public async Task DeleteTagCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}tag_01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}tag_02.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}tag_1000.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDB();

        // Add 2 series with cover images
        var s = DbFactory.Series("Test 1");
        s.Metadata.CollectionTags = new List<CollectionTag>();
        s.Metadata.CollectionTags.Add(new CollectionTag()
        {
            Title = "Something",
            CoverImage ="tag_01.jpg"
        });
        s.CoverImage = "series_01.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);

        s = DbFactory.Series("Test 2");
        s.Metadata.CollectionTags = new List<CollectionTag>();
        s.Metadata.CollectionTags.Add(new CollectionTag()
        {
            Title = "Something 2",
            CoverImage ="tag_02.jpg"
        });
        s.CoverImage = "series_03.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);


        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteTagCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }

    #endregion

    #region CleanupCacheDirectory

    [Fact]
    public void CleanupCacheDirectory_ClearAllFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}02.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        cleanupService.CleanupCacheDirectory();
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public void CleanupCacheDirectory_ClearAllFilesInSubDirectory()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}subdir/02.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        cleanupService.CleanupCacheDirectory();
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption: SearchOption.AllDirectories));
    }

    #endregion

    #region CleanupBackups

    [Fact]
    public void CleanupBackups_LeaveOneFile_SinceAllAreExpired()
    {
        var filesystem = CreateFileSystem();
        var filesystemFile = new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
        };
        filesystem.AddFile($"{BackupDirectory}kavita_backup_11_29_2021_12_00_13 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}kavita_backup_12_3_2021_9_27_58 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}randomfile.zip", filesystemFile);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        cleanupService.CleanupBackups();
        Assert.Single(ds.GetFiles(BackupDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public void CleanupBackups_LeaveLestExpired()
    {
        var filesystem = CreateFileSystem();
        var filesystemFile = new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
        };
        filesystem.AddFile($"{BackupDirectory}kavita_backup_11_29_2021_12_00_13 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}kavita_backup_12_3_2021_9_27_58 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}randomfile.zip", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(14))
        });

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        cleanupService.CleanupBackups();
        Assert.True(filesystem.File.Exists($"{BackupDirectory}randomfile.zip"));
    }

    #endregion
}
