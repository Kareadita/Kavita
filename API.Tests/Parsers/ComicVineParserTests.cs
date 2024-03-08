using API.Services;
using API.Services.Tasks.Scanner.Parser;
using NSubstitute;
using Xunit;

namespace API.Tests.Parsers;

public class ComicVineParserTests
{
    private readonly IDefaultParser _parser;
    public ComicVineParserTests()
    {
        var ds = Substitute.For<IDirectoryService>();
        _parser = new ComicVineParser(ds);
    }

    #region Parse

    /// <summary>
    /// Tests that when Series and Volume are filled out, Kavita uses that for the Series Name
    /// </summary>
    [Fact]
    public void Parse_SeriesWithComicInfo()
    {

    }

    /// <summary>
    /// Tests that no ComicInfo, take the Directory Name if it matches "Series (2002)" or "Series (2)"
    /// </summary>
    [Fact]
    public void Parse_SeriesWithDirectoryNameAsSeriesYear()
    {

    }

    /// <summary>
    /// Tests that no ComicInfo, take a directory name up to root if it matches "Series (2002)" or "Series (2)"
    /// </summary>
    [Fact]
    public void Parse_SeriesWithADirectoryNameAsSeriesYear()
    {

    }

    /// <summary>
    /// Tests that no ComicInfo and nothing matches Series (Volume), then just take the directory name as the Series
    /// </summary>
    [Fact]
    public void Parse_FallbackToDirectoryNameOnly()
    {

    }
    #endregion

    #region IsApplicable
    /// <summary>
    /// Tests that this Parser can only be used on ComicVine type
    /// </summary>
    [Fact]
    public void IsApplicable_Fails_WhenNonMatchingLibraryType()
    {

    }

    /// <summary>
    /// Tests that this Parser can only be used on ComicVine type
    /// </summary>
    [Fact]
    public void IsApplicable_Success_WhenMatchingLibraryType()
    {

    }
    #endregion
}
