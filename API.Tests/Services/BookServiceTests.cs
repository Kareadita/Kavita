using System;
using System.IO;
using System.IO.Compression;
using API.Entities.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using VersOne.Epub;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services
{
    public class BookServiceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IBookService _bookService;
        private readonly ILogger<BookService> _logger = Substitute.For<ILogger<BookService>>();
        private readonly IArchiveService _archiveService;
        private readonly ILogger<ArchiveService> archiveServiceLogger = Substitute.For<ILogger<ArchiveService>>();
        private readonly ILogger<DirectoryService> directoryServiceLogger = Substitute.For<ILogger<DirectoryService>>();
        
        public BookServiceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _archiveService = new ArchiveService(archiveServiceLogger);
            _bookService = new BookService(_logger, _archiveService, new DirectoryService(directoryServiceLogger));
        }

        [Theory]
        [InlineData("The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub", 16)]
        [InlineData("Non-existent file.epub", 0)]
        [InlineData("Non an ebub.pdf", 0)]
        public void GetNumberOfPagesTest(string filePath, int expectedPages)
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService/EPUB");
            Assert.Equal(expectedPages, _bookService.GetNumberOfPages(Path.Join(testDirectory, filePath)));
        }

        [Theory]
        [InlineData("<a href=\"../Styles/chapter01.xhtml\"/>", "../Styles/chapter01.xhtml")]
        [InlineData("<h1 class=\"copyright-title\"><a href=\"../Text/toc.xhtml#toc-copyright\">Copyright</a></h1>", "../Text/toc.xhtml#toc-copyright")]
        public void StylesheetHrefParse(string content, string expected)
        {
            Assert.Equal(expected, BookService.GetHrefKey(content));
        }

        // [Fact]
        // public void CanExtract_Test()
        // {
        //     var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService/EPUB");
        //     var file = "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub";
        //     using var archive = ZipFile.OpenRead(Path.Join(testDirectory, file));
        //     archive.ExtractToDirectory(testDirectory);
        // }
        
        
        [Fact]
        public void CanExtract_Test()
        {
            var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService/EPUB");
            var file = Path.Join(testDirectory, "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub");
            
            // var epubBook = EpubReader.ReadBook(file);
            // foreach (var contentFile in epubBook.ReadingOrder)
            // {
            //     _testOutputHelper.WriteLine(contentFile.Content);
            // }
        }
    }
}