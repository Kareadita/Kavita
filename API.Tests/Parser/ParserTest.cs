using API.Entities.Enums;
using Xunit;
using static API.Parser.Parser;

namespace API.Tests.Parser
{
    public class ParserTests
    {
        [Theory]
        [InlineData("Joe Shmo, Green Blue", "Joe Shmo, Green Blue")]
        [InlineData("Shmo, Joe",  "Shmo, Joe")]
        [InlineData("  Joe Shmo  ",  "Joe Shmo")]
        public void CleanAuthorTest(string input, string expected)
        {
            Assert.Equal(expected, CleanAuthor(input));
        }

        [Theory]
        [InlineData("Beastars - SP01", true)]
        [InlineData("Beastars SP01", true)]
        [InlineData("Beastars Special 01", false)]
        [InlineData("Beastars Extra 01", false)]
        [InlineData("Batman Beyond - Return of the Joker (2001) SP01", true)]
        public void HasSpecialTest(string input, bool expected)
        {
            Assert.Equal(expected,  HasSpecialMarker(input));
        }

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
        [InlineData("Hello_I_am_here", false, "Hello I am here")]
        [InlineData("Hello_I_am_here   ",  false, "Hello I am here")]
        [InlineData("[ReleaseGroup] The Title", false, "The Title")]
        [InlineData("[ReleaseGroup]_The_Title", false, "The Title")]
        [InlineData("-The Title", false, "The Title")]
        [InlineData("- The Title", false, "The Title")]
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1", false, "Kasumi Otoko no Ko v1.1")]
        [InlineData("Batman - Detective Comics - Rebirth Deluxe Edition Book 04 (2019) (digital) (Son of Ultron-Empire)", true, "Batman - Detective Comics - Rebirth Deluxe Edition")]
        public void CleanTitleTest(string input, bool isComic, string expected)
        {
            Assert.Equal(expected, CleanTitle(input, isComic));
        }


        // [Theory]
        // //[InlineData("@font-face{font-family:\"PaytoneOne\";src:url(\"..\\/Fonts\\/PaytoneOne.ttf\")}", "@font-face{font-family:\"PaytoneOne\";src:url(\"PaytoneOne.ttf\")}")]
        // [InlineData("@font-face{font-family:\"PaytoneOne\";src:url(\"..\\/Fonts\\/PaytoneOne.ttf\")}", "..\\/Fonts\\/PaytoneOne.ttf")]
        // //[InlineData("@font-face{font-family:'PaytoneOne';src:url('..\\/Fonts\\/PaytoneOne.ttf')}", "@font-face{font-family:'PaytoneOne';src:url('PaytoneOne.ttf')}")]
        // //[InlineData("@font-face{\r\nfont-family:'PaytoneOne';\r\nsrc:url('..\\/Fonts\\/PaytoneOne.ttf')\r\n}", "@font-face{font-family:'PaytoneOne';src:url('PaytoneOne.ttf')}")]
        // public void ReplaceStyleUrlTest(string input, string expected)
        // {
        //     var replacementStr = "PaytoneOne.ttf";
        //     // Use Match to validate since replace is weird
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
        [InlineData("test.pdf", true)]
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
        [InlineData("Citrus", "citrus")]
        [InlineData("Citrus+", "citrus+")]
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
        [InlineData("test.webp", true)]
        public void IsImageTest(string filename, bool expected)
        {
            Assert.Equal(expected, IsImage(filename));
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
        [InlineData("._Love Hina/Love Hina/", true)]
        [InlineData("@Recently-Snapshot/Love Hina/", true)]
        public void HasBlacklistedFolderInPathTest(string inputPath, bool expected)
        {
            Assert.Equal(expected, HasBlacklistedFolderInPath(inputPath));
        }
    }
}
