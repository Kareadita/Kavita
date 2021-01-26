using System.IO;
using System.IO.Compression;
using API.Interfaces;
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
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService");
            var file = Path.Join(testDirectory, archivePath);
            using ZipArchive archive = ZipFile.OpenRead(file);
            Assert.Equal(expected, _archiveService.ArchiveNeedsFlattening(archive));
        }
        
        [Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        public void GetCoverImageTest(string inputFile, string expectedOutputFile)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/CoverImageTests");
            var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));
            Assert.Equal(expectedBytes, _archiveService.GetCoverImage(Path.Join(testDirectory, inputFile)));
        }
    }
}