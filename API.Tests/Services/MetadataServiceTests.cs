﻿using System;
using System.IO;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class MetadataServiceTests
    {
        private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
        private readonly MetadataService _metadataService;
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IImageService _imageService = Substitute.For<IImageService>();
        private readonly IBookService _bookService = Substitute.For<IBookService>();
        private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        private readonly ILogger<MetadataService> _logger = Substitute.For<ILogger<MetadataService>>();

        public MetadataServiceTests()
        {
            _metadataService = new MetadataService(_unitOfWork, _logger, _archiveService, _bookService, _imageService);
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnFirstRun()
        {
            // Represents first run
            Assert.True(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = DateTime.Now
            }, false, false));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnFirstRunSeries()
        {
            // Represents first run
            Assert.True(MetadataService.ShouldUpdateCoverImage(null,null, false, false));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnSecondRun_FileModified()
        {
            // Represents first run
            Assert.True(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = new FileInfo(Path.Join(_testDirectory, "file in folder.zip")).LastWriteTime.Subtract(TimeSpan.FromDays(1))
            }, false, false));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnSecondRun_CoverImageLocked()
        {
            // Represents first run
            Assert.False(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = new FileInfo(Path.Join(_testDirectory, "file in folder.zip")).LastWriteTime
            }, false, true));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnSecondRun_ForceUpdate()
        {
            // Represents first run
            Assert.True(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = new FileInfo(Path.Join(_testDirectory, "file in folder.zip")).LastWriteTime
            }, true, false));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnSecondRun_NoFileChangeButNoCoverImage()
        {
            // Represents first run
            Assert.True(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = new FileInfo(Path.Join(_testDirectory, "file in folder.zip")).LastWriteTime
            }, false, false));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnSecondRun_FileChangeButNoCoverImage()
        {
            // Represents first run
            Assert.True(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = new FileInfo(Path.Join(_testDirectory, "file in folder.zip")).LastWriteTime + TimeSpan.FromDays(1)
            }, false, false));
        }

        [Fact]
        public void ShouldUpdateCoverImage_OnSecondRun_CoverImageSet()
        {
            // Represents first run
            Assert.False(MetadataService.ShouldUpdateCoverImage(new byte[] {1}, new MangaFile()
            {
                FilePath = Path.Join(_testDirectory, "file in folder.zip"),
                LastModified = new FileInfo(Path.Join(_testDirectory, "file in folder.zip")).LastWriteTime
            }, false, false));
        }
    }
}
