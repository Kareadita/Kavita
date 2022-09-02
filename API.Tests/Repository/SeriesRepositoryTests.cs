using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Repository;

public class SeriesRepositoryTests
{
    private readonly IUnitOfWork _unitOfWork;

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public SeriesRepositoryTests()
    {
        var contextOptions = new DbContextOptionsBuilder().UseSqlite(CreateInMemoryDatabase()).Options;
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

        await Seed.SeedSettings(_context,
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        _context.ServerSetting.Update(setting);

        var lib = new Library()
        {
            Name = "Manga", Folders = new List<FolderPath>() {new FolderPath() {Path = "C:/data/"}}
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                lib
            }
        });

        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.AppUserRating.RemoveRange(_context.AppUserRating.ToList());
        _context.Genre.RemoveRange(_context.Genre.ToList());
        _context.CollectionTag.RemoveRange(_context.CollectionTag.ToList());
        _context.Person.RemoveRange(_context.Person.ToList());

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

    private async Task SetupSeriesData()
    {
        var library = new Library()
        {
            Name = "Manga",
            Type = LibraryType.Manga,
            Folders = new List<FolderPath>()
            {
                new FolderPath() {Path = "C:/data/manga/"}
            }
        };

        var s = DbFactory.Series("The Idaten Deities Know Only Peace", "Heion Sedai no Idaten-tachi");
        s.Format = MangaFormat.Archive;

        library.Series = new List<Series>()
        {
            s,
        };

        _unitOfWork.LibraryRepository.Add(library);
        await _unitOfWork.CommitAsync();
    }


    [InlineData("Heion Sedai no Idaten-tachi", "", MangaFormat.Archive, "The Idaten Deities Know Only Peace")] // Matching on localized name in DB
    [InlineData("Heion Sedai no Idaten-tachi", "", MangaFormat.Pdf, null)]
    public async Task GetFullSeriesByAnyName_Should(string seriesName, string localizedName, string? expected)
    {
        var firstSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        var series =
            await _unitOfWork.SeriesRepository.GetFullSeriesByAnyName(seriesName, localizedName,
                1, MangaFormat.Unknown);
        if (expected == null)
        {
            Assert.Null(series);
        }
        else
        {
            Assert.NotNull(series);
            Assert.Equal(expected, series.Name);
        }
    }

}
