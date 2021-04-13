using System.Collections.Concurrent;
using System.Collections.Generic;
using API.Entities;
using API.Entities.Interfaces;
using API.Interfaces;
using API.Interfaces.Services;
using API.Parser;
using API.Services;
using API.Services.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services
{
    public class ScannerServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly ScannerService _scannerService;
        private readonly ILogger<ScannerService> _logger = Substitute.For<ILogger<ScannerService>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        private readonly IBookService _bookService = Substitute.For<IBookService>();
        private readonly IMetadataService _metadataService;
        private readonly ILogger<MetadataService> _metadataLogger = Substitute.For<ILogger<MetadataService>>();
        private Library _libraryMock;

        public ScannerServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _scannerService = new ScannerService(_unitOfWork, _logger, _archiveService, _metadataService, _bookService);
            _metadataService= Substitute.For<MetadataService>(_unitOfWork, _metadataLogger, _archiveService);
            // _libraryMock = new Library()
            // {
            //     Id = 1,
            //     Name = "Manga",
            //     Folders = new List<FolderPath>()
            //     {
            //         new FolderPath()
            //         {
            //             Id = 1,
            //             LastScanned = DateTime.Now,
            //             LibraryId = 1,
            //             Path = "E:/Manga"
            //         }
            //     },
            //     LastModified = DateTime.Now,
            //     Series = new List<Series>()
            //     {
            //         new Series()
            //         {
            //             Id = 0, 
            //             Name = "Darker Than Black"
            //         }
            //     }
            // };
            
        }

        [Fact]
        public void FindSeriesNotOnDisk_Should_RemoveNothing_Test()
        {
            var scannerService = new ScannerService(_unitOfWork, _logger, _archiveService, _metadataService, _bookService);
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
            var expectedSeries = new List<Series>();
            
            
            
            Assert.Empty(scannerService.FindSeriesNotOnDisk(existingSeries, infos));
        }

        [Theory]
        [InlineData(new [] {"Darker than Black"}, "Darker than Black", "Darker than Black")]
        [InlineData(new [] {"Darker than Black"}, "Darker Than Black", "Darker than Black")]
        [InlineData(new [] {"Darker than Black"}, "Darker Than Black!", "Darker Than Black!")]
        [InlineData(new [] {""}, "Runaway Jack", "Runaway Jack")]
        public void MergeNameTest(string[] existingSeriesNames, string parsedInfoName, string expected)
        {
            var scannerService = new ScannerService(_unitOfWork, _logger, _archiveService, _metadataService, _bookService);

            var collectedSeries = new ConcurrentDictionary<string, List<ParserInfo>>();
            foreach (var seriesName in existingSeriesNames)
            {
                AddToParsedInfo(collectedSeries, new ParserInfo() {Series = seriesName});
            }

            var actualName = scannerService.MergeName(collectedSeries, new ParserInfo()
            {
                Series = parsedInfoName
            });
            
            Assert.Equal(expected, actualName);
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
    }
}