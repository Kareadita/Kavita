using System;
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
            // ReSharper disable once CollectionNeverQueried.Local
            var files = new List<string>();
            var fileCount = DirectoryService.TraverseTreeParallelForEach(testDirectory, s => files.Add(s),
                API.Parser.Parser.ArchiveFileExtensions, _logger);

            Assert.Equal(28, fileCount);
        }

        [Fact]
        public void GetFiles_WithCustomRegex_ShouldPass_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/regex");
            var files = DirectoryService.GetFiles(testDirectory, @"file\d*.txt");
            Assert.Equal(2, files.Count());
        }

        [Fact]
        public void GetFiles_TopLevel_ShouldBeEmpty_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService");
            var files = DirectoryService.GetFiles(testDirectory);
            Assert.Empty(files);
        }

        [Fact]
        public void GetFilesWithExtensions_ShouldBeEmpty_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/extensions");
            var files = DirectoryService.GetFiles(testDirectory, "*.txt");
            Assert.Empty(files);
        }

        [Fact]
        public void GetFilesWithExtensions_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/extension");
            var files = DirectoryService.GetFiles(testDirectory, ".cbz|.rar");
            Assert.Equal(3, files.Count());
        }

        [Fact]
        public void GetFilesWithExtensions_BadDirectory_ShouldBeEmpty_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/DirectoryService/doesntexist");
            var files = DirectoryService.GetFiles(testDirectory, ".cbz|.rar");
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
        [InlineData(new [] {"C:/Manga/"}, new [] {"C:/Manga/Love Hina/Vol. 01.cbz"}, "C:/Manga/Love Hina")]
        public void FindHighestDirectoriesFromFilesTest(string[] rootDirectories, string[] folders, string expectedDirectory)
        {
            var actual = DirectoryService.FindHighestDirectoriesFromFiles(rootDirectories, folders);
            var expected = new Dictionary<string, string> {{expectedDirectory, ""}};
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("C:/Manga/", "C:/Manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
        [InlineData("C:/Manga/", "C:/Manga/Love Hina/Specials/Omake", "Omake,Specials,Love Hina")]
        [InlineData("C:/Manga", "C:/Manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
        [InlineData("C:/Manga", @"C:\Manga\Love Hina\Specials\Omake\", "Omake,Specials,Love Hina")]
        [InlineData(@"/manga/", @"/manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
        [InlineData(@"/manga/", @"/manga/", "")]
        [InlineData(@"E:\test", @"E:\test\Sweet X Trouble\Sweet X Trouble - Chapter 001.cbz", "Sweet X Trouble")]
        [InlineData(@"C:\/mount/gdrive/Library/Test Library/Comics/", @"C:\/mount/gdrive/Library/Test Library/Comics\godzilla rivals vs hedorah\vol 1\", "vol 1,godzilla rivals vs hedorah")]
        [InlineData(@"/manga/", @"/manga/Btooom!/Vol.1 Chapter 2/1.cbz", "Vol.1 Chapter 2,Btooom!")]
        [InlineData(@"C:/", @"C://Btooom!/Vol.1 Chapter 2/1.cbz", "Vol.1 Chapter 2,Btooom!")]
        [InlineData(@"C:\\", @"C://Btooom!/Vol.1 Chapter 2/1.cbz", "Vol.1 Chapter 2,Btooom!")]
        [InlineData(@"C://mount/gdrive/Library/Test Library/Comics", @"C://mount/gdrive/Library/Test Library/Comics/Dragon Age/Test", "Test,Dragon Age")]
        [InlineData(@"M:\", @"M:\Toukyou Akazukin\Vol. 01 Ch. 005.cbz", @"Toukyou Akazukin")]
        public void GetFoldersTillRoot_Test(string rootPath, string fullpath, string expectedArray)
        {
            var expected = expectedArray.Split(",");
            if (expectedArray.Equals(string.Empty))
            {
              expected = Array.Empty<string>();
            }
            Assert.Equal(expected, DirectoryService.GetFoldersTillRoot(rootPath, fullpath));
        }
    }
}
