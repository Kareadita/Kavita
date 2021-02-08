using System;
using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Interfaces;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class ScannerServiceTests
    {
        private readonly ScannerService _scannerService;
        private readonly ILogger<ScannerService> _logger = Substitute.For<ILogger<ScannerService>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        //private readonly IDirectoryService _directoryService = Substitute.For<DirectoryService>();
        private Library _libraryMock;

        public ScannerServiceTests()
        {
            _scannerService = new ScannerService(_unitOfWork, _logger, _archiveService);
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

        [Fact]
        public void ExistingOrDefault_Should_BeFromLibrary()
        {
            var allSeries = new List<Series>()
            {
                new Series() {Id = 2, Name = "Darker Than Black"},
                new Series() {Id = 3, Name = "Darker Than Black - Some Extension"},
                new Series() {Id = 4, Name = "Akame Ga Kill"},
            };
            Assert.Equal(_libraryMock.Series.ElementAt(0).Id, ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Darker Than Black").Id);
        }
        
        [Fact]
        public void ExistingOrDefault_Should_BeFromAllSeries()
        {
            var allSeries = new List<Series>()
            {
                new Series() {Id = 2, Name = "Darker Than Black"},
                new Series() {Id = 3, Name = "Darker Than Black - Some Extension"},
                new Series() {Id = 4, Name = "Akame Ga Kill"},
            };
            Assert.Equal(3, ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Darker Than Black - Some Extension").Id);
        }
        
        [Fact]
        public void ExistingOrDefault_Should_BeNull()
        {
            var allSeries = new List<Series>()
            {
                new Series() {Id = 2, Name = "Darker Than Black"},
                new Series() {Id = 3, Name = "Darker Than Black - Some Extension"},
                new Series() {Id = 4, Name = "Akame Ga Kill"},
            };
            Assert.Null(ScannerService.ExistingOrDefault(_libraryMock, allSeries, "Non existing series"));
        }

        // [Fact]
        // public void ScanLibrary_Should_Skip()
        // {
        //
        Library lib = new Library()
        {
            Id = 1,
            Name = "Darker Than Black",
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
            LastModified = DateTime.Now
        };
        //
        //     _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1).Returns(lib);
        //     
        //     _scannerService.ScanLibrary(1, false);
        // }
        
    }
}