using System.IO;
using Xunit;

namespace API.Tests.Services
{
    public class ImageProviderTest
    {
        [Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        public void GetCoverImageTest(string inputFile, string expectedOutputFile)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ImageProvider");
            var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));
            // TODO: Implement this with ScannerService
            //Assert.Equal(expectedBytes, ImageProvider.GetCoverImage(Path.Join(testDirectory, inputFile)));
        }
    }
}