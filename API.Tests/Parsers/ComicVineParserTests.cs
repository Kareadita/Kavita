using System.IO.Abstractions.TestingHelpers;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Parsers;

public class ComicVineParserTests
{
    private readonly ComicVineParser _parser;
    private readonly ILogger<DirectoryService> _dsLogger = Substitute.For<ILogger<DirectoryService>>();
    private const string RootDirectory = "C:/Comics/";

    public ComicVineParserTests()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Comics/");
        fileSystem.AddDirectory("C:/Comics/Birds of Prey (2002)");
        fileSystem.AddFile("C:/Comics/Birds of Prey (2002)/Birds of Prey 001 (2002).cbz", new MockFileData(""));
        fileSystem.AddFile("C:/Comics/DC Comics/Birds of Prey (1999)/Birds of Prey 001 (1999).cbz", new MockFileData(""));
        fileSystem.AddFile("C:/Comics/DC Comics/Blood Syndicate/Blood Syndicate 001 (1999).cbz", new MockFileData(""));
        var ds = new DirectoryService(_dsLogger, fileSystem);
        _parser = new ComicVineParser(ds);
    }

    #region Parse

    /// <summary>
    /// Tests that when Series and Volume are filled out, Kavita uses that for the Series Name
    /// </summary>
    [Fact]
    public void Parse_SeriesWithComicInfo()
    {
        var actual = _parser.Parse("C:/Comics/Birds of Prey (2002)/Birds of Prey 001 (2002).cbz", "C:/Comics/Birds of Prey (2002)/",
            RootDirectory, LibraryType.ComicVine, new ComicInfo()
            {
                Series = "Birds of Prey",
                Volume = "2002"
            });

        Assert.NotNull(actual);
        Assert.Equal("Birds of Prey (2002)", actual.Series);
        Assert.Equal("2002", actual.Volumes);
    }

    /// <summary>
    /// Tests that no ComicInfo, take the Directory Name if it matches "Series (2002)" or "Series (2)"
    /// </summary>
    [Fact]
    public void Parse_SeriesWithDirectoryNameAsSeriesYear()
    {
        var actual = _parser.Parse("C:/Comics/Birds of Prey (2002)/Birds of Prey 001 (2002).cbz", "C:/Comics/Birds of Prey (2002)/",
            RootDirectory, LibraryType.ComicVine, null);

        Assert.NotNull(actual);
        Assert.Equal("Birds of Prey (2002)", actual.Series);
        Assert.Equal("2002", actual.Volumes);
        Assert.Equal("1", actual.Chapters);
    }

    /// <summary>
    /// Tests that no ComicInfo, take a directory name up to root if it matches "Series (2002)" or "Series (2)"
    /// </summary>
    [Fact]
    public void Parse_SeriesWithADirectoryNameAsSeriesYear()
    {
        var actual = _parser.Parse("C:/Comics/DC Comics/Birds of Prey (1999)/Birds of Prey 001 (1999).cbz", "C:/Comics/DC Comics/",
            RootDirectory, LibraryType.ComicVine, null);

        Assert.NotNull(actual);
        Assert.Equal("Birds of Prey (1999)", actual.Series);
        Assert.Equal("1999", actual.Volumes);
        Assert.Equal("1", actual.Chapters);
    }

    /// <summary>
    /// Tests that no ComicInfo and nothing matches Series (Volume), then just take the directory name as the Series
    /// </summary>
    [Fact]
    public void Parse_FallbackToDirectoryNameOnly()
    {
        var actual = _parser.Parse("C:/Comics/DC Comics/Blood Syndicate/Blood Syndicate 001 (1999).cbz", "C:/Comics/DC Comics/",
            RootDirectory, LibraryType.ComicVine, null);

        Assert.NotNull(actual);
        Assert.Equal("Blood Syndicate", actual.Series);
        Assert.Equal(Parser.LooseLeafVolume, actual.Volumes);
        Assert.Equal("1", actual.Chapters);
    }
    #endregion

    #region IsApplicable
    /// <summary>
    /// Tests that this Parser can only be used on ComicVine type
    /// </summary>
    [Fact]
    public void IsApplicable_Fails_WhenNonMatchingLibraryType()
    {
        Assert.False(_parser.IsApplicable("", LibraryType.Comic));
    }

    /// <summary>
    /// Tests that this Parser can only be used on ComicVine type
    /// </summary>
    [Fact]
    public void IsApplicable_Success_WhenMatchingLibraryType()
    {
        Assert.True(_parser.IsApplicable("", LibraryType.ComicVine));
    }
    #endregion
}
