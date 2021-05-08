using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{

    public class DirectoryServiceTests
    {
        private readonly DirectoryService _directoryService;
        private readonly ILogger<DirectoryService> _logger = Substitute.For<ILogger<DirectoryService>>();

        public DirectoryServiceTests()
        {
            _directoryService = new DirectoryService(_logger);
        }

        [Fact]
        public void GetFilesTest_Should_Be28()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/Manga");
            var files = new List<string>();
            var fileCount = DirectoryService.TraverseTreeParallelForEach(testDirectory, s => files.Add(s),
                API.Parser.Parser.ArchiveFileExtensions, _logger);
            
            Assert.Equal(28, fileCount);
        }

        [Fact]
        public void GetFiles_WithCustomRegex_ShouldPass_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/regex");
            var files = _directoryService.GetFiles(testDirectory, @"file\d*.txt");
            Assert.Equal(2, files.Count());
        }
        
        [Fact]
        public void GetFiles_TopLevel_ShouldBeEmpty_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService");
            var files = _directoryService.GetFiles(testDirectory);
            Assert.Empty(files);
        }
        
        [Fact]
        public void GetFilesWithExtensions_ShouldBeEmpty_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/extensions");
            var files = _directoryService.GetFiles(testDirectory, "*.txt");
            Assert.Empty(files);
        }
        
        [Fact]
        public void GetFilesWithExtensions_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/extension");
            var files = _directoryService.GetFiles(testDirectory, ".cbz|.rar");
            Assert.Equal(3, files.Count());
        }
        
        [Fact]
        public void GetFilesWithExtensions_BadDirectory_ShouldBeEmpty_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/doesntexist");
            var files = _directoryService.GetFiles(testDirectory, ".cbz|.rar");
            Assert.Empty(files);
        }

        [Fact]
        public void ListDirectory_SubDirectory_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/");
            var dirs = _directoryService.ListDirectory(testDirectory);
            Assert.Contains(dirs, s => s.Contains("regex"));

        }
        
        [Fact]
        public void ListDirectory_NoSubDirectory_Test()
        {
            var dirs = _directoryService.ListDirectory("");
            Assert.DoesNotContain(dirs, s => s.Contains("regex"));

        }

        [Theory]
        [InlineData("C:/Manga/", "C:/Manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
        [InlineData("C:/Manga/", "C:/Manga/Love Hina/Specials/Omake", "Omake,Specials,Love Hina")]
        [InlineData("C:/Manga", "C:/Manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
        [InlineData("C:/Manga", @"C:\Manga\Love Hina\Specials\Omake\", "Omake,Specials,Love Hina")]
        [InlineData(@"/manga/", @"/manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
        public void GetFoldersTillRoot_Test(string rootPath, string fullpath, string expectedArray)
        {
            var expected = expectedArray.Split(",");
            Assert.Equal(expected, DirectoryService.GetFoldersTillRoot(rootPath, fullpath));
        }
    }
}