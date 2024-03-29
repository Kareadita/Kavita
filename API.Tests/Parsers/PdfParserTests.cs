using System.IO.Abstractions.TestingHelpers;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Parsers;

public class PdfParserTests
{
    private readonly PdfParser _parser;
    private readonly ILogger<DirectoryService> _dsLogger = Substitute.For<ILogger<DirectoryService>>();
    private const string RootDirectory = "C:/Books/";

    public PdfParserTests()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Books/");
        fileSystem.AddDirectory("C:/Books/Birds of Prey (2002)");
        fileSystem.AddFile("C:/Books/A Dictionary of Japanese Food - Ingredients and Culture/A Dictionary of Japanese Food - Ingredients and Culture.pdf", new MockFileData(""));
        fileSystem.AddFile("C:/Comics/DC Comics/Birds of Prey/Chapter 01/01.jpg", new MockFileData(""));
        var ds = new DirectoryService(_dsLogger, fileSystem);
        _parser = new PdfParser(ds);
    }

    #region Parse

    /// <summary>
    /// Tests that if there is a Series Folder then Chapter folder, the code appropriately identifies the Series name and Chapter
    /// </summary>
    [Fact]
    public void Parse_Book_SeriesWithDirectoryName()
    {
        var actual = _parser.Parse("C:/Books/A Dictionary of Japanese Food - Ingredients and Culture/A Dictionary of Japanese Food - Ingredients and Culture.pdf",
            "C:/Books/A Dictionary of Japanese Food - Ingredients and Culture/",
            RootDirectory, LibraryType.Book, null);

        Assert.NotNull(actual);
        Assert.Equal("A Dictionary of Japanese Food - Ingredients and Culture", actual.Series);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.True(actual.IsSpecial);
    }

    #endregion

    #region IsApplicable
    /// <summary>
    /// Tests that this Parser can only be used on pdfs
    /// </summary>
    [Fact]
    public void IsApplicable_Fails_WhenNonMatchingLibraryType()
    {
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Manga));
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Image));
        Assert.False(_parser.IsApplicable("something.epub", LibraryType.Image));
        Assert.False(_parser.IsApplicable("something.png", LibraryType.Book));
    }

    /// <summary>
    /// Tests that this Parser can only be used on pdfs
    /// </summary>
    [Fact]
    public void IsApplicable_Success_WhenMatchingLibraryType()
    {
        Assert.True(_parser.IsApplicable("something.pdf", LibraryType.Book));
        Assert.True(_parser.IsApplicable("something.pdf", LibraryType.Manga));
    }
    #endregion
}
