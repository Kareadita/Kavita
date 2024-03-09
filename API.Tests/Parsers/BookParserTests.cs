using System.IO.Abstractions.TestingHelpers;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Parsers;

public class BookParserTests
{
    private readonly BookParser _parser;
    private readonly ILogger<DirectoryService> _dsLogger = Substitute.For<ILogger<DirectoryService>>();
    private const string RootDirectory = "C:/Books/";

    public BookParserTests()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Books/");
        fileSystem.AddFile("C:/Books/Harry Potter/Harry Potter - Vol 1.epub", new MockFileData(""));
        fileSystem.AddFile("C:/Books/Adam Freeman - Pro ASP.NET Core 6.epub", new MockFileData(""));
        fileSystem.AddFile("C:/Books/My Fav Book SP01.epub", new MockFileData(""));
        var ds = new DirectoryService(_dsLogger, fileSystem);
        _parser = new BookParser(ds, Substitute.For<IBookService>(), new BasicParser(ds, new ImageParser(ds)));
    }

    #region Parse

    // TODO: I'm not sure how to actually test this as it relies on an epub parser to actually do anything

    /// <summary>
    /// Tests that if there is a Series Folder then Chapter folder, the code appropriately identifies the Series name and Chapter
    /// </summary>
    // [Fact]
    // public void Parse_SeriesWithDirectoryName()
    // {
    //     var actual = _parser.Parse("C:/Books/Harry Potter/Harry Potter - Vol 1.epub", "C:/Books/Birds of Prey/",
    //         RootDirectory, LibraryType.Book, new ComicInfo()
    //         {
    //             Series = "Harry Potter",
    //             Volume = "1"
    //         });
    //
    //     Assert.NotNull(actual);
    //     Assert.Equal("Harry Potter", actual.Series);
    //     Assert.Equal("1", actual.Volumes);
    // }

    #endregion

    #region IsApplicable
    /// <summary>
    /// Tests that this Parser can only be used on images and Image library type
    /// </summary>
    [Fact]
    public void IsApplicable_Fails_WhenNonMatchingLibraryType()
    {
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Manga));
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Book));

    }

    /// <summary>
    /// Tests that this Parser can only be used on images and Image library type
    /// </summary>
    [Fact]
    public void IsApplicable_Success_WhenMatchingLibraryType()
    {
        Assert.True(_parser.IsApplicable("something.epub", LibraryType.Image));
    }
    #endregion
}
