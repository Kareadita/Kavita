using Xunit;

namespace API.Tests.Parser;

public class MagazineParserTests
{
    [Theory]
    [InlineData("3D World - 2018  UK", "3D World")]
    public void ParseSeriesTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseMagazineSeries(filename));
    }

    // [Theory]
    // [InlineData("Harrison, Kim - Dates from Hell - Hollows Vol 2.5.epub", "2.5")]
    // public void ParseVolumeTest(string filename, string expected)
    // {
    //     Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseMagazineVolume(filename));
    // }
}
