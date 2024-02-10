using Xunit;

namespace API.Tests.Parser;

public class MagazineParserTests
{
    [Theory]
    [InlineData("3D World - 2018  UK", "3D World")]
    [InlineData("3D World - 2018", "3D World")]
    [InlineData("UK World - 022012 [Digital]", "UK World")]
    [InlineData("Computer Weekly - September 2023", "Computer Weekly")]
    public void ParseSeriesTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseMagazineSeries(filename));
    }

    [Theory]
    [InlineData("UK World - 022012 [Digital]", "2012")]
    [InlineData("Computer Weekly - September 2023", "2023")]
    [InlineData("Computer Weekly - September 2023 #2", "2023")]
    [InlineData("PC Games - 2001 #01", "2001")]
    public void ParseVolumeTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseMagazineVolume(filename));
    }

    [Theory]
    [InlineData("UK World - 022012 [Digital]", "0")]
    [InlineData("Computer Weekly - September 2023", "9")]
    [InlineData("Computer Weekly - September 2023 #2", "2")]
    [InlineData("PC Games - 2001 #01", "1")]
    public void ParseChapterTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseMagazineChapter(filename));
    }

    // [Theory]
    // [InlineData("AIR International Vol. 14 No. 3 (ISSN 1011-3250)", "1011-3250")]
    // public void ParseGTINTest(string filename, string expected)
    // {
    //     Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseGTIN(filename));
    // }
}
