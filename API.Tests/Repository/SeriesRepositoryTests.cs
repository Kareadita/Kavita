using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Repository;

#nullable enable

public class SeriesRepositoryTests
{
    private readonly IUnitOfWork _unitOfWork;

    private readonly DbConnection? _connection;
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
        _unitOfWork = new UnitOfWork(_context, mapper, null!);
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

        var lib = new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder("C:/data/").Build())
            .Build();

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
        var library = new LibraryBuilder("GetFullSeriesByAnyName Manga", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("C:/data/manga/").Build())
            .WithSeries(new SeriesBuilder("The Idaten Deities Know Only Peace")
                .WithLocalizedName("Heion Sedai no Idaten-tachi")
                .WithFormat(MangaFormat.Archive)
                .Build())
            .WithSeries(new SeriesBuilder("Hitomi-chan is Shy With Strangers")
                .WithLocalizedName("Hitomi-chan wa Hitomishiri")
                .WithFormat(MangaFormat.Archive)
                .Build())
            .Build();

        _unitOfWork.LibraryRepository.Add(library);
        await _unitOfWork.CommitAsync();
    }


    [Theory]
    [InlineData("The Idaten Deities Know Only Peace", MangaFormat.Archive, "", "The Idaten Deities Know Only Peace")] // Matching on series name in DB
    [InlineData("Heion Sedai no Idaten-tachi", MangaFormat.Archive, "The Idaten Deities Know Only Peace", "The Idaten Deities Know Only Peace")] // Matching on localized name in DB
    [InlineData("Heion Sedai no Idaten-tachi", MangaFormat.Pdf, "", null)]
    [InlineData("Hitomi-chan wa Hitomishiri", MangaFormat.Archive, "", "Hitomi-chan is Shy With Strangers")]
    public async Task GetFullSeriesByAnyName_Should(string seriesName, MangaFormat format, string localizedName, string? expected)
    {
        await ResetDb();
        await SetupSeriesData();

        var series =
            await _unitOfWork.SeriesRepository.GetFullSeriesByAnyName(seriesName, localizedName,
                2, format, false);
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

    // TODO: GetSeriesDtoForLibraryIdV2Async Tests (On Deck)

}
