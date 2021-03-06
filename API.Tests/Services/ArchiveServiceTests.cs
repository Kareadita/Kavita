﻿using System.IO;
using System.IO.Compression;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class ArchiveServiceTests
    {
        private readonly IArchiveService _archiveService;
        private readonly ILogger<ArchiveService> _logger = Substitute.For<ILogger<ArchiveService>>();

        public ArchiveServiceTests()
        {
            _archiveService = new ArchiveService(_logger);
        }

        [Theory]
        [InlineData("flat file.zip", false)]
        [InlineData("file in folder in folder.zip", true)]
        [InlineData("file in folder.zip", true)]
        [InlineData("file in folder_alt.zip", true)]
        public void ArchiveNeedsFlatteningTest(string archivePath, bool expected)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            var file = Path.Join(testDirectory, archivePath);
            using ZipArchive archive = ZipFile.OpenRead(file);
            Assert.Equal(expected, _archiveService.ArchiveNeedsFlattening(archive));
        }

        [Theory]
        [InlineData("non existent file.zip", false)]
        [InlineData("wrong extension.rar", false)]
        [InlineData("empty.zip", false)]
        [InlineData("flat file.zip", true)]
        [InlineData("file in folder in folder.zip", true)]
        [InlineData("file in folder.zip", true)]
        [InlineData("file in folder_alt.zip", true)]
        public void IsValidArchiveTest(string archivePath, bool expected)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            Assert.Equal(expected, _archiveService.IsValidArchive(Path.Join(testDirectory, archivePath)));
        }
        
        [Theory]
        [InlineData("non existent file.zip", 0)]
        [InlineData("wrong extension.rar", 0)]
        [InlineData("empty.zip", 0)]
        [InlineData("flat file.zip", 1)]
        [InlineData("file in folder in folder.zip", 1)]
        [InlineData("file in folder.zip", 1)]
        [InlineData("file in folder_alt.zip", 1)]
        public void GetNumberOfPagesFromArchiveTest(string archivePath, int expected)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            Assert.Equal(expected, _archiveService.GetNumberOfPagesFromArchive(Path.Join(testDirectory, archivePath)));
        }
        
        [Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        public void GetCoverImageTest(string inputFile, string expectedOutputFile)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/CoverImages");
            var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));
            Assert.Equal(expectedBytes, _archiveService.GetCoverImage(Path.Join(testDirectory, inputFile)));
        }
    }
}