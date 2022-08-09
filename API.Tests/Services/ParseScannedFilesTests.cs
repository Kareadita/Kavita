using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Parser;
using API.Services;
using API.Services.Tasks.Scanner;
using API.SignalR;
using API.Tests.Helpers;
using AutoMapper;
using DotNet.Globbing;
using Flurl.Util;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

internal class MockReadingItemService : IReadingItemService
{
    private readonly IDefaultParser _defaultParser;

    public MockReadingItemService(IDefaultParser defaultParser)
    {
        _defaultParser = defaultParser;
    }

    public ComicInfo GetComicInfo(string filePath)
    {
        return null;
    }

    public int GetNumberOfPages(string filePath, MangaFormat format)
    {
        return 1;
    }

    public string GetCoverImage(string fileFilePath, string fileName, MangaFormat format)
    {
        return string.Empty;
    }

    public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
    {
        throw new System.NotImplementedException();
    }

    public ParserInfo Parse(string path, string rootPath, LibraryType type)
    {
        return _defaultParser.Parse(path, rootPath, type);
    }

    public ParserInfo ParseFile(string path, string rootPath, LibraryType type)
    {
        return _defaultParser.Parse(path, rootPath, type);
    }
}

public class ParseScannedFilesTests
{
    private readonly ILogger<ParseScannedFiles> _logger = Substitute.For<ILogger<ParseScannedFiles>>();
    private readonly IUnitOfWork _unitOfWork;

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public ParseScannedFilesTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        _unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);

        // Since ProcessFile relies on _readingItemService, we can implement our own versions of _readingItemService so we have control over how the calls work
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

        _context.ServerSetting.Update(setting);

        _context.Library.Add(new Library()
        {
            Name = "Manga",
            Folders = new List<FolderPath>()
            {
                new FolderPath()
                {
                    Path = DataDirectory
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
        fileSystem.AddDirectory(DataDirectory);

        return fileSystem;
    }

    #endregion

    #region GetInfosByName

    [Fact]
    public void GetInfosByName_ShouldReturnGivenMatchingSeriesName()
    {
        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var infos = new List<ParserInfo>()
        {
            ParserInfoFactory.CreateParsedInfo("Accel World", "1", "0", "Accel World v1.cbz", false),
            ParserInfoFactory.CreateParsedInfo("Accel World", "2", "0", "Accel World v2.cbz", false)
        };
        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>
        {
            {
                new ParsedSeries()
                {
                    Format = MangaFormat.Archive,
                    Name = "Accel World",
                    NormalizedName = API.Parser.Parser.Normalize("Accel World")
                },
                infos
            },
            {
                new ParsedSeries()
                {
                    Format = MangaFormat.Pdf,
                    Name = "Accel World",
                    NormalizedName = API.Parser.Parser.Normalize("Accel World")
                },
                new List<ParserInfo>()
            }
        };

        var series = DbFactory.Series("Accel World");
        series.Format = MangaFormat.Pdf;

        Assert.Empty(ParseScannedFiles.GetInfosByName(parsedSeries, series));

        series.Format = MangaFormat.Archive;
        Assert.Equal(2, ParseScannedFiles.GetInfosByName(parsedSeries, series).Count());

    }

    [Fact]
    public void GetInfosByName_ShouldReturnGivenMatchingNormalizedSeriesName()
    {
        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var infos = new List<ParserInfo>()
        {
            ParserInfoFactory.CreateParsedInfo("Accel World", "1", "0", "Accel World v1.cbz", false),
            ParserInfoFactory.CreateParsedInfo("Accel World", "2", "0", "Accel World v2.cbz", false)
        };
        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>
        {
            {
                new ParsedSeries()
                {
                    Format = MangaFormat.Archive,
                    Name = "Accel World",
                    NormalizedName = API.Parser.Parser.Normalize("Accel World")
                },
                infos
            },
            {
                new ParsedSeries()
                {
                    Format = MangaFormat.Pdf,
                    Name = "Accel World",
                    NormalizedName = API.Parser.Parser.Normalize("Accel World")
                },
                new List<ParserInfo>()
            }
        };

        var series = DbFactory.Series("accel world");
        series.Format = MangaFormat.Archive;
        Assert.Equal(2, ParseScannedFiles.GetInfosByName(parsedSeries, series).Count());

    }

    #endregion

    // #region MergeName
    //
    // [Fact]
    // public async Task MergeName_ShouldMergeMatchingFormatAndName()
    // {
    //     var fileSystem = new MockFileSystem();
    //     fileSystem.AddDirectory("C:/Data/");
    //     fileSystem.AddFile("C:/Data/Accel World v1.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.pdf", new MockFileData(string.Empty));
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
    //     var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
    //         new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());
    //
    //
    //     await psf.ScanLibrariesForSeries(LibraryType.Manga, new List<string>() {"C:/Data/"}, "libraryName");
    //
    //     Assert.Equal("Accel World", psf.MergeName(ParserInfoFactory.CreateParsedInfo("Accel World", "1", "0", "Accel World v1.cbz", false)));
    //     Assert.Equal("Accel World", psf.MergeName(ParserInfoFactory.CreateParsedInfo("accel_world", "1", "0", "Accel World v1.cbz", false)));
    //     Assert.Equal("Accel World", psf.MergeName(ParserInfoFactory.CreateParsedInfo("accelworld", "1", "0", "Accel World v1.cbz", false)));
    // }
    //
    // [Fact]
    // public async Task MergeName_ShouldMerge_MismatchedFormatSameName()
    // {
    //     var fileSystem = new MockFileSystem();
    //     fileSystem.AddDirectory("C:/Data/");
    //     fileSystem.AddFile("C:/Data/Accel World v1.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.pdf", new MockFileData(string.Empty));
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
    //     var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
    //         new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());
    //
    //
    //     await psf.ScanLibrariesForSeries(LibraryType.Manga, new List<string>() {"C:/Data/"}, "libraryName");
    //
    //     Assert.Equal("Accel World",
    //         psf.MergeName(ParserInfoFactory.CreateParsedInfo("Accel World", "1", "0", "Accel World v1.epub", false)));
    //     Assert.Equal("Accel World",
    //         psf.MergeName(ParserInfoFactory.CreateParsedInfo("accel_world", "1", "0", "Accel World v1.epub", false)));
    // }
    //
    // #endregion

    #region ScanLibrariesForSeries

    [Fact]
    public async Task ScanLibrariesForSeries_ShouldFindFiles()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddFile("C:/Data/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Nothing.pdf", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        void TrackFiles(Tuple<bool, IList<ParserInfo>> parsedInfo)
        {
            var skippedScan = parsedInfo.Item1;
            var parsedFiles = parsedInfo.Item2;
            if (parsedFiles.Count == 0) return;

            var foundParsedSeries = new ParsedSeries()
            {
                Name = parsedFiles.First().Series,
                NormalizedName = API.Parser.Parser.Normalize(parsedFiles.First().Series),
                Format = parsedFiles.First().Format
            };

            parsedSeries.Add(foundParsedSeries, parsedFiles);
        }


        await psf.ScanLibrariesForSeries(LibraryType.Manga,
            new List<string>() {"C:/Data/"}, "libraryName", false, await _unitOfWork.SeriesRepository.GetFolderPathMap(1), TrackFiles);


        Assert.Equal(3, parsedSeries.Values.Count);
        Assert.NotEmpty(parsedSeries.Keys.Where(p => p.Format == MangaFormat.Archive && p.Name.Equals("Accel World")));
    }

    #endregion


    #region ProcessFiles

    private MockFileSystem CreateTestFilesystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Black World/Black World SP01.cbz", new MockFileData(string.Empty));

        return fileSystem;
    }

    [Fact]
    public async Task ProcessFiles_ForLibraryMode_OnlyCallsFolderActionForEachTopLevelFolder()
    {
        var fileSystem = CreateTestFilesystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var directoriesSeen = new HashSet<string>();
        await psf.ProcessFiles("C:/Data/", true, await _unitOfWork.SeriesRepository.GetFolderPathMap(1),
            (files, directoryPath) =>
        {
            directoriesSeen.Add(directoryPath);
            return Task.CompletedTask;
        });

        Assert.Equal(2, directoriesSeen.Count);
    }

    [Fact]
    public async Task ProcessFiles_ForNonLibraryMode_CallsFolderActionOnce()
    {
        var fileSystem = CreateTestFilesystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var directoriesSeen = new HashSet<string>();
        await psf.ProcessFiles("C:/Data/", false, await _unitOfWork.SeriesRepository.GetFolderPathMap(1),(files, directoryPath) =>
        {
            directoriesSeen.Add(directoryPath);
            return Task.CompletedTask;
        });

        Assert.Equal(1, directoriesSeen.Count);
        directoriesSeen.TryGetValue("C:/Data/", out var actual);
        Assert.Equal("C:/Data/", actual);
    }

    [Fact]
    public async Task ProcessFiles_ShouldCallFolderActionTwice()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Black World/Black World SP01.cbz", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var callCount = 0;
        await psf.ProcessFiles("C:/Data", true, await _unitOfWork.SeriesRepository.GetFolderPathMap(1),(files, folderPath) =>
        {
            callCount++;

            return Task.CompletedTask;
        });

        Assert.Equal(2, callCount);
    }


    /// <summary>
    /// Due to this not being a library, it's going to consider everything under C:/Data as being one folder aka a series folder
    /// </summary>
    [Fact]
    public async Task ProcessFiles_ShouldCallFolderActionOnce()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Black World/Black World SP01.cbz", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());

        var callCount = 0;
        await psf.ProcessFiles("C:/Data", false, await _unitOfWork.SeriesRepository.GetFolderPathMap(1),(files, folderPath) =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, callCount);
    }

    #endregion
}
