﻿using Xunit;

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

        [Theory]
        [InlineData("@font-face{font-family:'syyskuu_repaleinen';src:url(data:font/opentype;base64,AAEAAAA", "@font-face{font-family:'syyskuu_repaleinen';src:url(data:font/opentype;base64,AAEAAAA")]
        [InlineData("@font-face{font-family:'syyskuu_repaleinen';src:url('fonts/font.css')", "@font-face{font-family:'syyskuu_repaleinen';src:url('TEST/fonts/font.css')")]
        public void ReplaceFontSrcUrl(string input, string expected)
        {
            var apiBase = "TEST/";
            var actual = API.Parser.Parser.FontSrcUrlRegex.Replace(input, "$1" + apiBase + "$2" + "$3");
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("@import url('font.css');", "@import url('TEST/font.css');")]
        public void ReplaceImportSrcUrl(string input, string expected)
        {
            var apiBase = "TEST/";
            var actual = API.Parser.Parser.CssImportUrlRegex.Replace(input, "$1" + apiBase + "$2" + "$3");
            Assert.Equal(expected, actual);
        }
    }
}
