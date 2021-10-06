using Xunit;

namespace API.Tests.Parser
{
    public class BookParserTests
    {
        [Theory]
        [InlineData("Gifting The Wonderful World With Blessings! - 3 Side Stories [yuNS][Unknown]", "Gifting The Wonderful World With Blessings!")]
        public void ParseSeriesTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseSeries(filename));
        }

        [Theory]
        [InlineData("Harrison, Kim - Dates from Hell - Hollows Vol 2.5.epub", "2.5")]
        public void ParseVolumeTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseVolume(filename));
        }
    }
}
