using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using API.Archive;
using API.Data.Metadata;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services
{
    public class ArchiveServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly ArchiveService _archiveService;
        private readonly ILogger<ArchiveService> _logger = Substitute.For<ILogger<ArchiveService>>();
        private readonly ILogger<DirectoryService> _directoryServiceLogger = Substitute.For<ILogger<DirectoryService>>();
        private readonly IDirectoryService _directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>());

        public ArchiveServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _archiveService = new ArchiveService(_logger, _directoryService);
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
        [InlineData("macos_native.zip", 21)]
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
        [InlineData(new [] {"folder.jpg"}, "folder.jpg")]
        [InlineData(new [] {"vol1/"}, "")]
        [InlineData(new [] {"folder.jpg", "vol1/folder.jpg"}, "folder.jpg")]
        [InlineData(new [] {"cover.jpg", "vol1/folder.jpg"}, "cover.jpg")]
        [InlineData(new [] {"__MACOSX/cover.jpg", "vol1/page 01.jpg"}, "")]
        [InlineData(new [] {"Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c060 (v10) - p200 [Digital] [LuCaZ].jpg", "folder.jpg"}, "folder.jpg")]
        public void FindFolderEntry(string[] files, string expected)
        {
            var foundFile = _archiveService.FindFolderEntry(files);
            Assert.Equal(expected, string.IsNullOrEmpty(foundFile) ? "" : foundFile);
        }

        [Theory]
        [InlineData(new [] {"folder.jpg"}, "folder.jpg")]
        [InlineData(new [] {"vol1/"}, "")]
        [InlineData(new [] {"folder.jpg", "vol1/folder.jpg"}, "folder.jpg")]
        [InlineData(new [] {"cover.jpg", "vol1/folder.jpg"}, "cover.jpg")]
        [InlineData(new [] {"page 2.jpg", "page 10.jpg"}, "page 2.jpg")]
        [InlineData(new [] {"__MACOSX/cover.jpg", "vol1/page 01.jpg"}, "vol1/page 01.jpg")]
        [InlineData(new [] {"Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c060 (v10) - p200 [Digital] [LuCaZ].jpg", "folder.jpg"}, "Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg")]
        [InlineData(new [] {"001.jpg", "001 - chapter 1/001.jpg"}, "001.jpg")]
        public void FindFirstEntry(string[] files, string expected)
        {
            var foundFile = ArchiveService.FirstFileEntry(files, string.Empty);
            Assert.Equal(expected, string.IsNullOrEmpty(foundFile) ? "" : foundFile);
        }



        // TODO: This is broken on GA due to DirectoryService.CoverImageDirectory
        //[Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        [InlineData("macos_native.zip", "macos_native.jpg")]
        [InlineData("v10 - duplicate covers.cbz", "v10 - duplicate covers.expected.jpg")]
        [InlineData("sorting.zip", "sorting.expected.jpg")]
        public void GetCoverImage_Default_Test(string inputFile, string expectedOutputFile)
        {
            var archiveService =  Substitute.For<ArchiveService>(_logger, new DirectoryService(_directoryServiceLogger));
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/CoverImages");
            var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));
            archiveService.Configure().CanOpen(Path.Join(testDirectory, inputFile)).Returns(ArchiveLibrary.Default);
            var sw = Stopwatch.StartNew();

            var outputDir = Path.Join(testDirectory, "output");
            DirectoryService.ClearAndDeleteDirectory(outputDir);
            DirectoryService.ExistOrCreate(outputDir);


            var coverImagePath = archiveService.GetCoverImage(Path.Join(testDirectory, inputFile),
                Path.GetFileNameWithoutExtension(inputFile) + "_output");
            var actual = File.ReadAllBytes(coverImagePath);


            Assert.Equal(expectedBytes, actual);
            _testOutputHelper.WriteLine($"Processed in {sw.ElapsedMilliseconds} ms");
            DirectoryService.ClearAndDeleteDirectory(outputDir);
        }


        // TODO: This is broken on GA due to DirectoryService.CoverImageDirectory
        //[Theory]
        [InlineData("v10.cbz", "v10.expected.jpg")]
        [InlineData("v10 - with folder.cbz", "v10 - with folder.expected.jpg")]
        [InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.jpg")]
        [InlineData("macos_native.zip", "macos_native.jpg")]
        [InlineData("v10 - duplicate covers.cbz", "v10 - duplicate covers.expected.jpg")]
        [InlineData("sorting.zip", "sorting.expected.jpg")]
        public void GetCoverImage_SharpCompress_Test(string inputFile, string expectedOutputFile)
        {
            var archiveService =  Substitute.For<ArchiveService>(_logger, new DirectoryService(_directoryServiceLogger));
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/CoverImages");
            var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));

            archiveService.Configure().CanOpen(Path.Join(testDirectory, inputFile)).Returns(ArchiveLibrary.SharpCompress);
            Stopwatch sw = Stopwatch.StartNew();
            Assert.Equal(expectedBytes, File.ReadAllBytes(archiveService.GetCoverImage(Path.Join(testDirectory, inputFile), Path.GetFileNameWithoutExtension(inputFile) + "_output")));
            _testOutputHelper.WriteLine($"Processed in {sw.ElapsedMilliseconds} ms");
        }

        // TODO: This is broken on GA due to DirectoryService.CoverImageDirectory
        //[Theory]
        [InlineData("Archives/macos_native.zip")]
        [InlineData("Formats/One File with DB_Supported.zip")]
        public void CanParseCoverImage(string inputFile)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/");
            Assert.NotEmpty(File.ReadAllBytes(_archiveService.GetCoverImage(Path.Join(testDirectory, inputFile), Path.GetFileNameWithoutExtension(inputFile) + "_output")));
        }

        [Fact]
        public void ShouldHaveComicInfo()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
            var archive = Path.Join(testDirectory, "file in folder.zip");
            var summaryInfo = "By all counts, Ryouta Sakamoto is a loser when he's not holed up in his room, bombing things into oblivion in his favorite online action RPG. But his very own uneventful life is blown to pieces when he's abducted and taken to an uninhabited island, where he soon learns the hard way that he's being pitted against others just like him in a explosives-riddled death match! How could this be happening? Who's putting them up to this? And why!? The name, not to mention the objective, of this very real survival game is eerily familiar to Ryouta, who has mastered its virtual counterpart-BTOOOM! Can Ryouta still come out on top when he's playing for his life!?";

            Assert.Equal(summaryInfo, _archiveService.GetComicInfo(archive).Summary);
        }

        [Fact]
        public void CanParseComicInfo()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
            var archive = Path.Join(testDirectory, "ComicInfo.zip");
            var actual = _archiveService.GetComicInfo(archive);
            var expected = new ComicInfo()
            {
                Publisher = "Yen Press",
                Genre = "Manga, Movies & TV",
                Summary =
                    "By all counts, Ryouta Sakamoto is a loser when he's not holed up in his room, bombing things into oblivion in his favorite online action RPG. But his very own uneventful life is blown to pieces when he's abducted and taken to an uninhabited island, where he soon learns the hard way that he's being pitted against others just like him in a explosives-riddled death match! How could this be happening? Who's putting them up to this? And why!? The name, not to mention the objective, of this very real survival game is eerily familiar to Ryouta, who has mastered its virtual counterpart-BTOOOM! Can Ryouta still come out on top when he's playing for his life!?",
                PageCount = 194,
                LanguageISO = "en",
                Notes = "Scraped metadata from Comixology [CMXDB450184]",
                Series = "BTOOOM!",
                Title = "v01",
                Web = "https://www.comixology.com/BTOOOM/digital-comic/450184"
            };

            Assert.NotStrictEqual(expected, actual);
        }
    }
}
