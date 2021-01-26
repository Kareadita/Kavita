using System.IO;
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
        private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService");
        public ScannerServiceTests()
        {
            _scannerService = new ScannerService(_unitOfWork, _logger);
        }

        [Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        public void GetCoverImageTest(string inputFile, string expectedOutputFile)
        {
            var expectedBytes = File.ReadAllBytes(Path.Join(_testDirectory, expectedOutputFile));
            Assert.Equal(expectedBytes, _scannerService.GetCoverImage(Path.Join(_testDirectory, inputFile)));
        }
    }
}