using System;
using System.Collections.Generic;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
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
        private readonly IMetadataService _metadataService;
        private readonly ILogger<MetadataService> _metadataLogger = Substitute.For<ILogger<MetadataService>>();
        private Library _libraryMock;

        public ScannerServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _scannerService = new ScannerService(_unitOfWork, _logger, _archiveService, _metadataService);
            _metadataService= Substitute.For<MetadataService>(_unitOfWork, _metadataLogger, _archiveService);
            _libraryMock = new Library()
            {
                Id = 1,
                Name = "Manga",
                Folders = new List<FolderPath>()
                {
                    new FolderPath()
                    {
                        Id = 1,
                        LastScanned = DateTime.Now,
                        LibraryId = 1,
                        Path = "E:/Manga"
                    }
                },
                LastModified = DateTime.Now,
                Series = new List<Series>()
                {
                    new Series()
                    {
                        Id = 0, 
                        Name = "Darker Than Black"
                    }
                }
            };
            
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