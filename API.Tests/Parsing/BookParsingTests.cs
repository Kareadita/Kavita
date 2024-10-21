using API.Entities.Enums;
using Xunit;

namespace API.Tests.Parsing;

public class BookParsingTests
{
    [Theory]
    [InlineData("Gifting The Wonderful World With Blessings! - 3 Side Stories [yuNS][Unknown]", "Gifting The Wonderful World With Blessings!")]
    [InlineData("BBC Focus 00 The Science of Happiness 2nd Edition (2018)", "BBC Focus 00 The Science of Happiness 2nd Edition")]
    [InlineData("Faust - Volume 01 [Del Rey][Scans_Compressed]", "Faust")]
    public void ParseSeriesTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseSeries(filename, LibraryType.Book));
    }

    [Theory]
    [InlineData("Harrison, Kim - Dates from Hell - Hollows Vol 2.5.epub", "2.5")]
    [InlineData("Faust - Volume 01 [Del Rey][Scans_Compressed]", "1")]
    public void ParseVolumeTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseVolume(filename, LibraryType.Book));
    }
}
