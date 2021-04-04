using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using API.Archive;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services
{
    public class ArchiveServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IArchiveService _archiveService;
        private readonly ILogger<ArchiveService> _logger = Substitute.For<ILogger<ArchiveService>>();

        public ArchiveServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
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
        [InlineData("winrar.rar", true)]
        [InlineData("empty.zip", true)]
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
        [InlineData("winrar.rar", 0)]
        [InlineData("empty.zip", 0)]
        [InlineData("flat file.zip", 1)]
        [InlineData("file in folder in folder.zip", 1)]
        [InlineData("file in folder.zip", 1)]
        [InlineData("file in folder_alt.zip", 1)]
        [InlineData("macos_none.zip", 0)]
        [InlineData("macos_one.zip", 1)]
        public void GetNumberOfPagesFromArchiveTest(string archivePath, int expected)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            var sw = Stopwatch.StartNew();
            Assert.Equal(expected, _archiveService.GetNumberOfPagesFromArchive(Path.Join(testDirectory, archivePath)));
            _testOutputHelper.WriteLine($"Processed Original in {sw.ElapsedMilliseconds} ms");
        }
        


        [Theory]
        [InlineData("non existent file.zip", ArchiveLibrary.NotSupported)]
        [InlineData("winrar.rar", ArchiveLibrary.SharpCompress)]
        [InlineData("empty.zip", ArchiveLibrary.Default)]
        [InlineData("flat file.zip", ArchiveLibrary.Default)]
        [InlineData("file in folder in folder.zip", ArchiveLibrary.Default)]
        [InlineData("file in folder.zip", ArchiveLibrary.Default)]
        [InlineData("file in folder_alt.zip", ArchiveLibrary.Default)]
        public void CanOpenArchive(string archivePath, ArchiveLibrary expected)
        {
            var sw = Stopwatch.StartNew();
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            
            Assert.Equal(expected, _archiveService.CanOpen(Path.Join(testDirectory, archivePath)));
            _testOutputHelper.WriteLine($"Processed Original in {sw.ElapsedMilliseconds} ms");
        }
        
        
        [Theory]
        [InlineData("non existent file.zip", 0)]
        [InlineData("winrar.rar", 0)]
        [InlineData("empty.zip", 0)]
        [InlineData("flat file.zip", 1)]
        [InlineData("file in folder in folder.zip", 1)]
        [InlineData("file in folder.zip", 1)]
        [InlineData("file in folder_alt.zip", 1)]
        public void CanExtractArchive(string archivePath, int expectedFileCount)
        {
            
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            var extractDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives/Extraction");

            DirectoryService.ClearAndDeleteDirectory(extractDirectory);
            
            Stopwatch sw = Stopwatch.StartNew();
            _archiveService.ExtractArchive(Path.Join(testDirectory, archivePath), extractDirectory);
            var di1 = new DirectoryInfo(extractDirectory);
            Assert.Equal(expectedFileCount, di1.Exists ? di1.GetFiles().Length : 0);
            _testOutputHelper.WriteLine($"Processed in {sw.ElapsedMilliseconds} ms");
            
            DirectoryService.ClearAndDeleteDirectory(extractDirectory);
        }
        
        
        
        [Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        //[InlineData("png.zip", "png.PNG")]
        public void GetCoverImageTest(string inputFile, string expectedOutputFile)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/CoverImages");
            var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));
            Stopwatch sw = Stopwatch.StartNew();
            Assert.Equal(expectedBytes, _archiveService.GetCoverImage(Path.Join(testDirectory, inputFile)));
            _testOutputHelper.WriteLine($"Processed in {sw.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData("Archives/06_v01[DMM].zip")]
        [InlineData("Formats/One File with DB_Supported.zip")]
        public void CanParseCoverImage(string inputFile)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/");
            Assert.NotEmpty(_archiveService.GetCoverImage(Path.Join(testDirectory, inputFile)));
        }

        [Fact]
        public void ShouldHaveComicInfo()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
            var archive = Path.Join(testDirectory, "file in folder.zip");
            var summaryInfo = "By all counts, Ryouta Sakamoto is a loser when he's not holed up in his room, bombing things into oblivion in his favorite online action RPG. But his very own uneventful life is blown to pieces when he's abducted and taken to an uninhabited island, where he soon learns the hard way that he's being pitted against others just like him in a explosives-riddled death match! How could this be happening? Who's putting them up to this? And why!? The name, not to mention the objective, of this very real survival game is eerily familiar to Ryouta, who has mastered its virtual counterpart-BTOOOM! Can Ryouta still come out on top when he's playing for his life!?";
            
            Assert.Equal(summaryInfo, _archiveService.GetSummaryInfo(archive));

        }
    }
}