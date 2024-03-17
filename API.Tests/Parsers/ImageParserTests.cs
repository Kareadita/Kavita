using System.IO.Abstractions.TestingHelpers;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Parsers;

public class ImageParserTests
{
    private readonly ImageParser _parser;
    private readonly ILogger<DirectoryService> _dsLogger = Substitute.For<ILogger<DirectoryService>>();
    private const string RootDirectory = "C:/Comics/";

    public ImageParserTests()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Comics/");
        fileSystem.AddDirectory("C:/Comics/Birds of Prey (2002)");
        fileSystem.AddFile("C:/Comics/Birds of Prey/Chapter 01/01.jpg", new MockFileData(""));
        fileSystem.AddFile("C:/Comics/DC Comics/Birds of Prey/Chapter 01/01.jpg", new MockFileData(""));
        var ds = new DirectoryService(_dsLogger, fileSystem);
        _parser = new ImageParser(ds);
    }

    #region Parse

    /// <summary>
    /// Tests that if there is a Series Folder then Chapter folder, the code appropriately identifies the Series name and Chapter
    /// </summary>
    [Fact]
    public void Parse_SeriesWithDirectoryName()
    {
        var actual = _parser.Parse("C:/Comics/Birds of Prey/Chapter 01/01.jpg", "C:/Comics/Birds of Prey/",
            RootDirectory, LibraryType.Image, null);

        Assert.NotNull(actual);
        Assert.Equal("Birds of Prey", actual.Series);
        Assert.Equal("1", actual.Chapters);
    }

    /// <summary>
    /// Tests that if there is a Series Folder only, the code appropriately identifies the Series name from folder
    /// </summary>
    [Fact]
    public void Parse_SeriesWithNoNestedChapter()
    {
        var actual = _parser.Parse("C:/Comics/Birds of Prey/Chapter 01 page 01.jpg", "C:/Comics/",
            RootDirectory, LibraryType.Image, null);

        Assert.NotNull(actual);
        Assert.Equal("Birds of Prey", actual.Series);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
    }

    /// <summary>
    /// Tests that if there is a Series Folder only, the code appropriately identifies the Series name from folder and everything else as a
    /// </summary>
    [Fact]
    public void Parse_SeriesWithLooseImages()
    {
        var actual = _parser.Parse("C:/Comics/Birds of Prey/page 01.jpg", "C:/Comics/",
            RootDirectory, LibraryType.Image, null);

        Assert.NotNull(actual);
        Assert.Equal("Birds of Prey", actual.Series);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.True(actual.IsSpecial);
    }


    #endregion

    #region IsApplicable
    /// <summary>
    /// Tests that this Parser can only be used on images and Image library type
    /// </summary>
    [Fact]
    public void IsApplicable_Fails_WhenNonMatchingLibraryType()
    {
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Manga));
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Image));
        Assert.False(_parser.IsApplicable("something.epub", LibraryType.Image));
    }

    /// <summary>
    /// Tests that this Parser can only be used on images and Image library type
    /// </summary>
    [Fact]
    public void IsApplicable_Success_WhenMatchingLibraryType()
    {
        Assert.True(_parser.IsApplicable("something.png", LibraryType.Image));
    }
    #endregion
}
