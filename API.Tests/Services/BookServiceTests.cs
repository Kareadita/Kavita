using System.IO;
using API.Interfaces;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class BookServiceTests
    {
        private readonly IBookService _bookService;
        private readonly ILogger<BookService> _logger = Substitute.For<ILogger<BookService>>();

        public BookServiceTests()
        {
            _bookService = new BookService(_logger);
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

    }
}