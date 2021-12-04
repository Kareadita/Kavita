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

namespace API.Tests.Services
{
    public class CacheServiceTests
    {
        private readonly ILogger<CacheService> _logger = Substitute.For<ILogger<CacheService>>();
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();

        private readonly DbConnection _connection;
        private readonly DataContext _context;

        private const string CacheDirectory = "C:/kavita/config/cache/";
        private const string CoverImageDirectory = "C:/kavita/config/covers/";
        private const string BackupDirectory = "C:/kavita/config/backups/";
        private const string DataDirectory = "C:/data/";

        public CacheServiceTests()
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
            fileSystem.AddDirectory(DataDirectory);

            return fileSystem;
        }

        #endregion

        #region Ensure

        [Fact]
        public async Task Ensure_DirectoryAlreadyExists_DontExtractAnything()
        {
            var filesystem = CreateFileSystem();
            filesystem.AddFile($"{DataDirectory}Test v1.zip", new MockFileData(""));
            filesystem.AddDirectory($"{CacheDirectory}1/");
            var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
            var cleanupService = new CacheService(_logger, _unitOfWork, ds,
                new ReadingItemService(Substitute.For<IArchiveService>(), Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds));

            await ResetDB();
            var s = DbFactory.Series("Test");
            var v = DbFactory.Volume("1");
            var c = new Chapter()
            {
                Number = "1",
                Files = new List<MangaFile>()
                {
                    new MangaFile()
                    {
                        Format = MangaFormat.Archive,
                        FilePath = $"{DataDirectory}Test v1.zip",
                    }
                }
            };
            v.Chapters.Add(c);
            s.Volumes.Add(v);
            s.LibraryId = 1;
            _context.Series.Add(s);

            await _context.SaveChangesAsync();

            await cleanupService.Ensure(1);
            Assert.Empty(ds.GetFiles(filesystem.Path.Join(CacheDirectory, "1"), searchOption:SearchOption.AllDirectories));
        }

        [Fact]
        public async Task Ensure_DirectoryAlreadyExists_ExtractsImages()
        {
            var filesystem = CreateFileSystem();
            filesystem.AddFile($"{DataDirectory}Test v1.zip", new MockFileData(""));
            filesystem.AddDirectory($"{CacheDirectory}1/");
            var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
            var cleanupService = new CacheService(_logger, _unitOfWork, ds,
                new ReadingItemService(Substitute.For<IArchiveService>(), Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds));

            await ResetDB();
            var s = DbFactory.Series("Test");
            var v = DbFactory.Volume("1");
            var c = new Chapter()
            {
                Number = "1",
                Files = new List<MangaFile>()
                {
                    new MangaFile()
                    {
                        Format = MangaFormat.Archive,
                        FilePath = $"{DataDirectory}Test v1.zip",
                    }
                }
            };
            v.Chapters.Add(c);
            s.Volumes.Add(v);
            s.LibraryId = 1;
            _context.Series.Add(s);

            await _context.SaveChangesAsync();

            await cleanupService.Ensure(1);
            Assert.Empty(ds.GetFiles(filesystem.Path.Join(CacheDirectory, "1"), searchOption:SearchOption.AllDirectories));
        }


        #endregion

        // [Fact]
        // public async void Ensure_ShouldExtractArchive(int chapterId)
        // {
        //
        //     // CacheDirectory needs to be customized.
        //     _unitOfWork.VolumeRepository.GetChapterAsync(chapterId).Returns(new Chapter
        //     {
        //         Id = 1,
        //         Files = new List<MangaFile>()
        //         {
        //             new MangaFile()
        //             {
        //                 FilePath = ""
        //             }
        //         }
        //     });
        //
        //     await _cacheService.Ensure(1);
        //
        //     var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/CacheService/Archives");
        //
        // }

        //string GetCachedPagePath(Volume volume, int page)
        // [Fact]
        // //[InlineData("", 0, "")]
        // public void GetCachedPagePathTest_Should()
        // {
        //
        //     // string archivePath = "flat file.zip";
        //     // int pageNum = 0;
        //     // string expected = "cache/1/pexels-photo-6551949.jpg";
        //     //
        //     // var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
        //     // var file = Path.Join(testDirectory, archivePath);
        //     // var volume = new Volume
        //     // {
        //     //     Id = 1,
        //     //     Files = new List<MangaFile>()
        //     //     {
        //     //         new()
        //     //         {
        //     //             Id = 1,
        //     //             Chapter = 0,
        //     //             FilePath = archivePath,
        //     //             Format = MangaFormat.Archive,
        //     //             Pages = 1,
        //     //         }
        //     //     },
        //     //     Name = "1",
        //     //     Number = 1
        //     // };
        //     //
        //     // var cacheService = Substitute.ForPartsOf<CacheService>();
        //     // cacheService.Configure().CacheDirectoryIsAccessible().Returns(true);
        //     // cacheService.Configure().GetVolumeCachePath(1, volume.Files.ElementAt(0)).Returns("cache/1/");
        //     // _directoryService.Configure().GetFilesWithExtension("cache/1/").Returns(new string[] {"pexels-photo-6551949.jpg"});
        //     // Assert.Equal(expected, _cacheService.GetCachedPagePath(volume, pageNum));
        //     //Assert.True(true);
        // }
        //
        // [Fact]
        // public void GetOrderedChaptersTest()
        // {
        //     // var files = new List<Chapter>()
        //     // {
        //     //     new()
        //     //     {
        //     //         Number = "1"
        //     //     },
        //     //     new()
        //     //     {
        //     //         Chapter = 2
        //     //     },
        //     //     new()
        //     //     {
        //     //         Chapter = 0
        //     //     },
        //     // };
        //     // var expected = new List<MangaFile>()
        //     // {
        //     //     new()
        //     //     {
        //     //         Chapter = 1
        //     //     },
        //     //     new()
        //     //     {
        //     //         Chapter = 2
        //     //     },
        //     //     new()
        //     //     {
        //     //         Chapter = 0
        //     //     },
        //     // };
        //     // Assert.NotStrictEqual(expected, _cacheService.GetOrderedChapters(files));
        // }
        //

    }
}
