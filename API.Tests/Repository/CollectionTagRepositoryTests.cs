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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Repository;

public class CollectionTagRepositoryTests
{
    private readonly IUnitOfWork _unitOfWork;

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public CollectionTagRepositoryTests()
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

    // #region RemoveTagsWithoutSeries
    //
    // [Fact]
    // public async Task RemoveTagsWithoutSeries_ShouldRemoveTags()
    // {
    //     var library = new LibraryBuilder("Test", LibraryType.Manga).Build();
    //     var series = new SeriesBuilder("Test 1").Build();
    //     var commonTag = new AppUserCollectionBuilder("Tag 1").Build();
    //     series.Metadata.CollectionTags.Add(commonTag);
    //     series.Metadata.CollectionTags.Add(new AppUserCollectionBuilder("Tag 2").Build());
    //
    //     var series2 = new SeriesBuilder("Test 1").Build();
    //     series2.Metadata.CollectionTags.Add(commonTag);
    //     library.Series.Add(series);
    //     library.Series.Add(series2);
    //     _unitOfWork.LibraryRepository.Add(library);
    //     await _unitOfWork.CommitAsync();
    //
    //     Assert.Equal(2, series.Metadata.CollectionTags.Count);
    //     Assert.Single(series2.Metadata.CollectionTags);
    //
    //     // Delete both series
    //     _unitOfWork.SeriesRepository.Remove(series);
    //     _unitOfWork.SeriesRepository.Remove(series2);
    //
    //     await _unitOfWork.CommitAsync();
    //
    //     // Validate that both tags exist
    //     Assert.Equal(2, (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).Count());
    //
    //     await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
    //
    //     Assert.Empty(await _unitOfWork.CollectionTagRepository.GetAllTagsAsync());
    // }
    //
    // [Fact]
    // public async Task RemoveTagsWithoutSeries_ShouldNotRemoveTags()
    // {
    //     var library = new LibraryBuilder("Test", LibraryType.Manga).Build();
    //     var series = new SeriesBuilder("Test 1").Build();
    //     var commonTag = new AppUserCollectionBuilder("Tag 1").Build();
    //     series.Metadata.CollectionTags.Add(commonTag);
    //     series.Metadata.CollectionTags.Add(new AppUserCollectionBuilder("Tag 2").Build());
    //
    //     var series2 = new SeriesBuilder("Test 1").Build();
    //     series2.Metadata.CollectionTags.Add(commonTag);
    //     library.Series.Add(series);
    //     library.Series.Add(series2);
    //     _unitOfWork.LibraryRepository.Add(library);
    //     await _unitOfWork.CommitAsync();
    //
    //     Assert.Equal(2, series.Metadata.CollectionTags.Count);
    //     Assert.Single(series2.Metadata.CollectionTags);
    //
    //     await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
    //
    //     // Validate that both tags exist
    //     Assert.Equal(2, (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).Count());
    // }
    //
    // #endregion
}
