using System.IO;
using System.IO.Abstractions;
using API.Services;
using EasyCaching.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class BookServiceTests
{
    private readonly IBookService _bookService;
    private readonly ILogger<BookService> _logger = Substitute.For<ILogger<BookService>>();

    public BookServiceTests()
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        _bookService = new BookService(_logger, directoryService,
            new ImageService(Substitute.For<ILogger<ImageService>>(), directoryService)
            , Substitute.For<IMediaErrorService>());
    }

    [Theory]
    [InlineData("The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub", 16)]
    [InlineData("Non-existent file.epub", 0)]
    [InlineData("Non an ebub.pdf", 0)]
    [InlineData("test_ſ.pdf", 1)] // This is dependent on Docnet bug https://github.com/GowenGit/docnet/issues/80
    [InlineData("test.pdf", 1)]
    public void GetNumberOfPagesTest(string filePath, int expectedPages)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        Assert.Equal(expectedPages, _bookService.GetNumberOfPages(Path.Join(testDirectory, filePath)));
    }

    [Fact]
    public void ShouldHaveComicInfo()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var archive = Path.Join(testDirectory, "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub");
        const string summaryInfo = "Book Description";

        var comicInfo = _bookService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal(summaryInfo, comicInfo.Summary);
        Assert.Equal("genre1, genre2", comicInfo.Genre);
    }

    [Fact]
    public void ShouldHaveComicInfo_WithAuthors()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var archive = Path.Join(testDirectory, "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub");

        var comicInfo = _bookService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("Roger Starbuck,Junya Inoue", comicInfo.Writer);
    }

    [Fact]
    public void ShouldParseAsVolumeGroup_WithoutSeriesIndex()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var archive = Path.Join(testDirectory, "TitleWithVolume_NoSeriesOrSeriesIndex.epub");

        var comicInfo = _bookService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("1", comicInfo.Volume);
        Assert.Equal("Accel World", comicInfo.Series);
    }

    [Fact]
    public void ShouldParseAsVolumeGroup_WithSeriesIndex()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var archive = Path.Join(testDirectory, "TitleWithVolume.epub");

        var comicInfo = _bookService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("1.0", comicInfo.Volume);
        Assert.Equal("Accel World", comicInfo.Series);
    }

    [Fact]
    public void ShouldHaveComicInfoForPdf()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var document = Path.Join(testDirectory, "test.pdf");
        var comicInfo = _bookService.GetComicInfo(document);
        Assert.NotNull(comicInfo);
        Assert.Equal("Variations Chromatiques de concert", comicInfo.Title);
        Assert.Equal("Georges Bizet \\(1838-1875\\)", comicInfo.Writer);
    }

    // TODO: Get the file from microtherion
    // [Fact]
    // public void ShouldUsePdfInfoDict()
    // {
    //     var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/Library/Books/PDFs");
    //     var document = Path.Join(testDirectory, "Rollo at Work SP01.pdf");
    //     var comicInfo = _bookService.GetComicInfo(document);
    //     Assert.NotNull(comicInfo);
    //     Assert.Equal("Rollo at Work", comicInfo.Title);
    //     Assert.Equal("Jacob Abbott", comicInfo.Writer);
    //     Assert.Equal(2008, comicInfo.Year);
    // }

    [Fact]
    public void ShouldHandleIndirectPdfObjects()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var document = Path.Join(testDirectory, "indirect.pdf");
        var comicInfo = _bookService.GetComicInfo(document);
        Assert.NotNull(comicInfo);
        Assert.Equal(2018, comicInfo.Year);
        Assert.Equal(8, comicInfo.Month);
    }

    [Fact]
    public void FailGracefullyWithEncryptedPdf()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
        var document = Path.Join(testDirectory, "encrypted.pdf");
        var comicInfo = _bookService.GetComicInfo(document);
        Assert.Null(comicInfo);
    }
}
