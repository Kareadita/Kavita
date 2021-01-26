using System.IO;
using System.IO.Compression;
using API.Extensions;
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

        private readonly string _testDirectory =
            Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService");

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
            var file = Path.Join(_testDirectory, archivePath);
            using ZipArchive archive = ZipFile.OpenRead(file);
            Assert.Equal(expected, _archiveService.ArchiveNeedsFlattening(archive));
        }
    }
}