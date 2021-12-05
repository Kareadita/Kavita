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
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

internal class MockReadingItemService : IReadingItemService
{
    public ComicInfo GetComicInfo(string filePath, MangaFormat format)
    {
        throw new System.NotImplementedException();
    }

    public int GetNumberOfPages(string filePath, MangaFormat format)
    {
        throw new System.NotImplementedException();
    }

    public string GetCoverImage(string fileFilePath, string fileName, MangaFormat format)
    {
        throw new System.NotImplementedException();
    }

    public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
    {
        throw new System.NotImplementedException();
    }

    public ParserInfo Parse(string path, string rootPath, LibraryType type)
    {
        throw new System.NotImplementedException();
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
    public void GetInfosByName()
    {

    }

    #endregion

    #region MergeName

    [Fact]
    public void MergeName_()
    {

    }

    #endregion
}
