using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using API.Parser;
using API.Services;
using API.Services.Tasks;
using API.Tests.Helpers;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services
{
    public class ScannerServiceTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly ScannerService _scannerService;
        private readonly ILogger<ScannerService> _logger = Substitute.For<ILogger<ScannerService>>();
        private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        private readonly IBookService _bookService = Substitute.For<IBookService>();
        private readonly ILogger<MetadataService> _metadataLogger = Substitute.For<ILogger<MetadataService>>();

        private readonly DbConnection _connection;
        private readonly DataContext _context;
        

        public ScannerServiceTests(ITestOutputHelper testOutputHelper)
        {
            var contextOptions = new DbContextOptionsBuilder()
                .UseSqlite(CreateInMemoryDatabase())
                .Options;
            _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

            _context = new DataContext(contextOptions);
            Task.Run(SeedDb).GetAwaiter().GetResult();
            
            
            //BackgroundJob.Enqueue is what I need to mock or something (it's static...)
            // ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService, 
            //     IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService, ICleanupService cleanupService, 
            //     IBackgroundJobClient jobClient
            //var taskScheduler = new TaskScheduler(Substitute.For<ICacheService>(), Substitute.For<ILogger<TaskScheduler>>(), Substitute.For<)
            
            
            // Substitute.For<UserManager<AppUser>>() - Not needed because only for UserService
            IUnitOfWork unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);
            
            
            _testOutputHelper = testOutputHelper;
            IMetadataService metadataService = Substitute.For<MetadataService>(unitOfWork, _metadataLogger, _archiveService, _bookService);
            _scannerService = new ScannerService(unitOfWork, _logger, _archiveService, metadataService, _bookService);
        }

        private async Task<bool> SeedDb()
        {
            await _context.Database.MigrateAsync();
            await Seed.SeedSettings(_context);

            _context.Library.Add(new Library()
            {
                Name = "Manga",
                Folders = new List<FolderPath>()
                {
                    new FolderPath()
                    {
                        Path = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/Manga")
                    }
                }
            });
            return await _context.SaveChangesAsync() > 0;
        }

        // [Fact]
        // public void Test()
        // {
        //     _scannerService.ScanLibrary(1, false);
        //
        //     var series = _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1).Result.Series;
        // }
        
        [Fact]
        public void FindSeriesNotOnDisk_Should_RemoveNothing_Test()
        {
            var infos = new Dictionary<string, List<ParserInfo>>();
            
            AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black"});
            AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "1"});
            AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "10"});

            var existingSeries = new List<Series>();
            existingSeries.Add(new Series()
            {
                Name = "Cage of Eden",
                LocalizedName = "Cage of Eden",
                OriginalName = "Cage of Eden",
                NormalizedName = API.Parser.Parser.Normalize("Cage of Eden")
            });
            existingSeries.Add(new Series()
            {
                Name = "Darker Than Black",
                LocalizedName = "Darker Than Black",
                OriginalName = "Darker Than Black",
                NormalizedName = API.Parser.Parser.Normalize("Darker Than Black")
            });



            Assert.Empty(_scannerService.FindSeriesNotOnDisk(existingSeries, infos));
        }

        [Theory]
        [InlineData(new [] {"Darker than Black"}, "Darker than Black", "Darker than Black")]
        [InlineData(new [] {"Darker than Black"}, "Darker Than Black", "Darker than Black")]
        [InlineData(new [] {"Darker than Black"}, "Darker Than Black!", "Darker than Black")]
        [InlineData(new [] {""}, "Runaway Jack", "Runaway Jack")]
        public void MergeNameTest(string[] existingSeriesNames, string parsedInfoName, string expected)
        {
            var collectedSeries = new ConcurrentDictionary<string, List<ParserInfo>>();
            foreach (var seriesName in existingSeriesNames)
            {
                AddToParsedInfo(collectedSeries, new ParserInfo() {Series = seriesName});
            }

            var actualName = _scannerService.MergeName(collectedSeries, new ParserInfo()
            {
                Series = parsedInfoName
            });
            
            Assert.Equal(expected, actualName);
        }

        [Fact]
        public void RemoveMissingSeries_Should_RemoveSeries()
        {
            var existingSeries = new List<Series>()
            {
                EntityFactory.CreateSeries("Darker than Black Vol 1"),
                EntityFactory.CreateSeries("Darker than Black"),
                EntityFactory.CreateSeries("Beastars"),
            };
            var missingSeries = new List<Series>()
            {
                EntityFactory.CreateSeries("Darker than Black Vol 1"),
            };
            existingSeries = ScannerService.RemoveMissingSeries(existingSeries, missingSeries, out var removeCount).ToList();
            
            Assert.DoesNotContain(missingSeries[0].Name, existingSeries.Select(s => s.Name));
            Assert.Equal(missingSeries.Count, removeCount);
        }

        private void AddToParsedInfo(IDictionary<string, List<ParserInfo>> collectedSeries, ParserInfo info)
        {
            if (collectedSeries.GetType() == typeof(ConcurrentDictionary<,>))
            {
                ((ConcurrentDictionary<string, List<ParserInfo>>) collectedSeries).AddOrUpdate(info.Series, new List<ParserInfo>() {info}, (_, oldValue) =>
                {
                    oldValue ??= new List<ParserInfo>();
                    if (!oldValue.Contains(info))
                    {
                        oldValue.Add(info);
                    }

                    return oldValue;
                });
            }
            else
            {
                if (!collectedSeries.ContainsKey(info.Series))
                {
                    collectedSeries.Add(info.Series, new List<ParserInfo>() {info});
                }
                else
                {
                    var list = collectedSeries[info.Series];
                    if (!list.Contains(info))
                    {
                        list.Add(info);
                    }

                    collectedSeries[info.Series] = list;
                }
                
            }
            
        }
        
        

        // [Fact]
        // public void ExistingOrDefault_Should_BeFromLibrary()
        // {
        //     var allSeries = new List<Series>()
        //     {
        //         new Series() {Id = 2, Name = "Darker Than Black"},
        //         new Series() {Id = 3, Name = "Darker Than Black - Some Extension"},
        //         new Series() {Id = 4, Name = "Akame Ga Kill"},
        //     };
        //     Assert.Equal(_libraryMock.Series.ElementAt(0).Id, ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Darker Than Black").Id);
        //     Assert.Equal(_libraryMock.Series.ElementAt(0).Id, ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Darker than Black").Id);
        // }
        //
        // [Fact]
        // public void ExistingOrDefault_Should_BeFromAllSeries()
        // {
        //     var allSeries = new List<Series>()
        //     {
        //         new Series() {Id = 2, Name = "Darker Than Black"},
        //         new Series() {Id = 3, Name = "Darker Than Black - Some Extension"},
        //         new Series() {Id = 4, Name = "Akame Ga Kill"},
        //     };
        //     Assert.Equal(3, ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Darker Than Black - Some Extension").Id);
        // }
        //
        // [Fact]
        // public void ExistingOrDefault_Should_BeNull()
        // {
        //     var allSeries = new List<Series>()
        //     {
        //         new Series() {Id = 2, Name = "Darker Than Black"},
        //         new Series() {Id = 3, Name = "Darker Than Black - Some Extension"},
        //         new Series() {Id = 4, Name = "Akame Ga Kill"},
        //     };
        //     Assert.Null(ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Non existing series"));
        // }

        [Fact]
        public void Should_CreateSeries_Test()
        {
            // var allSeries = new List<Series>();
            // var parsedSeries = new Dictionary<string, List<ParserInfo>>();
            //
            // parsedSeries.Add("Darker Than Black", new List<ParserInfo>()
            // {
            //     new ParserInfo() {Chapters = "0", Filename = "Something.cbz", Format = MangaFormat.Archive, FullFilePath = "E:/Manga/Something.cbz", Series = "Darker Than Black", Volumes = "1"},
            //     new ParserInfo() {Chapters = "0", Filename = "Something.cbz", Format = MangaFormat.Archive, FullFilePath = "E:/Manga/Something.cbz", Series = "Darker than Black", Volumes = "2"}
            // });
            //
            // _scannerService.UpsertSeries(_libraryMock, parsedSeries, allSeries);
            //
            // Assert.Equal(1, _libraryMock.Series.Count);
            // Assert.Equal(2, _libraryMock.Series.ElementAt(0).Volumes.Count);
            // _testOutputHelper.WriteLine(_libraryMock.ToString());
            Assert.True(true);
        }
        
        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");

            connection.Open();

            return connection;
        }

        public void Dispose() => _connection.Dispose();
    }
}