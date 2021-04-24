using API.Services;
using Xunit;

namespace API.Tests.Parser
{
    public class BookParserTests
    {
        [Theory]
        [InlineData("Stephen King - 2011 - 11_22_63.epub", "Stephen King")]
        public void ParseSeriesTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseSeries(filename));
        }
        
        [Theory]
        [InlineData("Stephen King - 2011 - 11_22_63.epub", "Stephen King")]
        public void ParseChaptersTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseChapter(filename) ?? BookService.ParseInfo(filename).Chapters);
        }
        
        [Theory]
        [InlineData("Stephen King - 2011 - 11_22_63.epub", "Stephen King")]
        public void ParseBookTest(string filename, string expected)
        {
            Assert.Equal(expected, BookService.ParseInfo(filename).Chapters);
        }
    }
}