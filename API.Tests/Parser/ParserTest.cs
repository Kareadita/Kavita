using Xunit;
using static API.Parser.Parser;

namespace API.Tests.Parser
{
    public class ParserTests
    {

        [Theory]
        [InlineData("0001", "1")]
        [InlineData("1", "1")]
        [InlineData("0013", "13")]
        public void RemoveLeadingZeroesTest(string input, string expected)
        {
            Assert.Equal(expected, RemoveLeadingZeroes(input));
        }

        [Theory]
        [InlineData("1", "001")]
        [InlineData("10", "010")]
        [InlineData("100", "100")]
        public void PadZerosTest(string input, string expected)
        {
            Assert.Equal(expected, PadZeros(input));
        }

        [Theory]
        [InlineData("Hello_I_am_here", "Hello I am here")]
        [InlineData("Hello_I_am_here   ", "Hello I am here")]
        [InlineData("[ReleaseGroup] The Title", "The Title")]
        [InlineData("[ReleaseGroup]_The_Title", "The Title")]
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1", "Kasumi Otoko no Ko v1.1")]
        public void CleanTitleTest(string input, string expected)
        {
            Assert.Equal(expected, CleanTitle(input));
        }
        
        
        // [Theory]
        // //[InlineData("@font-face{font-family:\"PaytoneOne\";src:url(\"..\\/Fonts\\/PaytoneOne.ttf\")}", "@font-face{font-family:\"PaytoneOne\";src:url(\"PaytoneOne.ttf\")}")]
        // [InlineData("@font-face{font-family:\"PaytoneOne\";src:url(\"..\\/Fonts\\/PaytoneOne.ttf\")}", "..\\/Fonts\\/PaytoneOne.ttf")]
        // //[InlineData("@font-face{font-family:'PaytoneOne';src:url('..\\/Fonts\\/PaytoneOne.ttf')}", "@font-face{font-family:'PaytoneOne';src:url('PaytoneOne.ttf')}")]
        // //[InlineData("@font-face{\r\nfont-family:'PaytoneOne';\r\nsrc:url('..\\/Fonts\\/PaytoneOne.ttf')\r\n}", "@font-face{font-family:'PaytoneOne';src:url('PaytoneOne.ttf')}")]
        // public void ReplaceStyleUrlTest(string input, string expected)
        // {
        //     var replacementStr = "PaytoneOne.ttf";
        //     // TODO: Use Match to validate since replace is weird
        //     //Assert.Equal(expected, FontSrcUrlRegex.Replace(input, "$1" + replacementStr + "$2" + "$3"));
        //     var match = FontSrcUrlRegex.Match(input);
        //     Assert.Equal(!string.IsNullOrEmpty(expected), FontSrcUrlRegex.Match(input).Success);
        // }

        
        [Theory]
        [InlineData("test.cbz", true)]
        [InlineData("test.cbr", true)]
        [InlineData("test.zip", true)]
        [InlineData("test.rar", true)]
        [InlineData("test.rar.!qb", false)]
        [InlineData("[shf-ma-khs-aqs]negi_pa_vol15007.jpg", false)]
        public void IsArchiveTest(string input, bool expected)
        {
            Assert.Equal(expected, IsArchive(input));
        }
        
        [Theory]
        [InlineData("test.epub", true)]
        [InlineData("test.pdf", false)]
        [InlineData("test.mobi", false)]
        [InlineData("test.djvu", false)]
        [InlineData("test.zip", false)]
        [InlineData("test.rar", false)]
        [InlineData("test.epub.!qb", false)]
        [InlineData("[shf-ma-khs-aqs]negi_pa_vol15007.ebub", false)]
        public void IsBookTest(string input, bool expected)
        {
            Assert.Equal(expected, IsBook(input));
        }
        
        [Theory]
        [InlineData("test.epub", true)]
        [InlineData("test.EPUB", true)]
        [InlineData("test.mobi", false)]
        [InlineData("test.epub.!qb", false)]
        [InlineData("[shf-ma-khs-aqs]negi_pa_vol15007.ebub", false)]
        public void IsEpubTest(string input, bool expected)
        {
            Assert.Equal(expected, IsEpub(input));
        }

        // [Theory]
        // [InlineData("Tenjou Tenge Omnibus", "Omnibus")]
        // [InlineData("Tenjou Tenge {Full Contact Edition}", "Full Contact Edition")]
        // [InlineData("Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz", "Full Contact Edition")]
        // [InlineData("Wotakoi - Love is Hard for Otaku Omnibus v01 (2018) (Digital) (danke-Empire)", "Omnibus")]
        // [InlineData("To Love Ru v01 Uncensored (Ch.001-007)", "Uncensored")]
        // [InlineData("Chobits Omnibus Edition v01 [Dark Horse]", "Omnibus Edition")]
        // [InlineData("[dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz", "Digital Colored Comics")]
        // [InlineData("AKIRA - c003 (v01) [Full Color] [Darkhorse].cbz", "Full Color")]
        // public void ParseEditionTest(string input, string expected)
        // {
        //     Assert.Equal(expected, ParseEdition(input));
        // }
        
        // [Theory]
        // [InlineData("Beelzebub Special OneShot - Minna no Kochikame x Beelzebub (2016) [Mangastream].cbz", true)]
        // [InlineData("Beelzebub_Omake_June_2012_RHS", true)]
        // [InlineData("Beelzebub_Side_Story_02_RHS.zip", false)]
        // [InlineData("Darker than Black Shikkoku no Hana Special [Simple Scans].zip", true)]
        // [InlineData("Darker than Black Shikkoku no Hana Fanbook Extra [Simple Scans].zip", true)]
        // [InlineData("Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Extra Chapter", true)]
        // [InlineData("Ani-Hina Art Collection.cbz", true)]
        // public void ParseMangaSpecialTest(string input, bool expected)
        // {
        //     Assert.Equal(expected, ParseMangaSpecial(input) != "");
        // }
        
        [Theory]
        [InlineData("12-14", 12)]
        [InlineData("24", 24)]
        [InlineData("18-04", 4)]
        [InlineData("18-04.5", 4.5)]
        [InlineData("40", 40)]
        [InlineData("40a-040b", 0)]
        [InlineData("40.1_a", 0)]
        public void MinimumNumberFromRangeTest(string input, float expected)
        {
            Assert.Equal(expected, MinimumNumberFromRange(input));
        }

        [Theory]
        [InlineData("Darker Than Black", "darkerthanblack")]
        [InlineData("Darker Than Black - Something", "darkerthanblacksomething")]
        [InlineData("Darker Than_Black", "darkerthanblack")]
        [InlineData("", "")]
        public void NormalizeTest(string input, string expected)
        {
            Assert.Equal(expected, Normalize(input));
        }
        
        

        [Theory]
        [InlineData("test.jpg", true)]
        [InlineData("test.jpeg", true)]
        [InlineData("test.png", true)]
        [InlineData(".test.jpg", false)]
        [InlineData("!test.jpg", false)]
        public void IsImageTest(string filename, bool expected)
        {
            Assert.Equal(expected, IsImage(filename));
        }
        
        [Theory]
        [InlineData("C:/", "C:/Love Hina/Love Hina - Special.cbz", "Love Hina")]
        [InlineData("C:/", "C:/Love Hina/Specials/Ani-Hina Art Collection.cbz", "Love Hina")]
        [InlineData("C:/", "C:/Mujaki no Rakuen Something/Mujaki no Rakuen Vol12 ch76.cbz", "Mujaki no Rakuen")]
        public void FallbackTest(string rootDir, string inputPath, string expectedSeries)
        {
            var actual = Parse(inputPath, rootDir);
            if (actual == null)
            {
                Assert.NotNull(actual);
                return;
            }
            
            Assert.Equal(expectedSeries, actual.Series);
        }
        
        [Theory]
        [InlineData("Love Hina - Special.jpg", false)]
        [InlineData("folder.jpg", true)]
        [InlineData("DearS_v01_cover.jpg", true)]
        [InlineData("DearS_v01_covers.jpg", false)]
        [InlineData("!cover.jpg", true)]
        [InlineData("cover.jpg", true)]
        [InlineData("cover.png", true)]
        [InlineData("ch1/cover.png", true)]
        public void IsCoverImageTest(string inputPath, bool expected)
        {
            Assert.Equal(expected, IsCoverImage(inputPath));
        }
        
        [Theory]
        [InlineData("__MACOSX/Love Hina - Special.jpg", true)]
        [InlineData("TEST/Love Hina - Special.jpg", false)]
        [InlineData("__macosx/Love Hina/", false)]
        [InlineData("MACOSX/Love Hina/", false)]
        public void HasBlacklistedFolderInPathTest(string inputPath, bool expected)
        {
            Assert.Equal(expected, HasBlacklistedFolderInPath(inputPath));
        }
    }
}