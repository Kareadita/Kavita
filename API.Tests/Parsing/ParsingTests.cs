using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using static API.Services.Tasks.Scanner.Parser.Parser;

namespace API.Tests.Parsing;

public class ParsingTests
{
    [Fact]
    public void ShouldWork()
    {
        var s = 6.5f + "";
        var a = float.Parse(s, CultureInfo.InvariantCulture);
        Assert.Equal(6.5f, a);
    }

    // [Theory]
    // [InlineData("de-DE")]
    // [InlineData("en-US")]
    // public void ShouldParse(string culture)
    // {
    //     var s = 6.5f + "";
    //     var a = float.Parse(s, CultureInfo.CreateSpecificCulture(culture));
    //     Assert.Equal(6.5f, a);
    // }

    [Theory]
    [InlineData("Joe Shmo, Green Blue", "Joe Shmo, Green Blue")]
    [InlineData("Shmo, Joe",  "Shmo, Joe")]
    [InlineData("  Joe Shmo  ",  "Joe Shmo")]
    public void CleanAuthorTest(string input, string expected)
    {
        Assert.Equal(expected, CleanAuthor(input));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("DEAD Tube Prologue", "DEAD Tube Prologue")]
    [InlineData("DEAD Tube Prologue SP01", "DEAD Tube Prologue")]
    [InlineData("DEAD_Tube_Prologue SP01", "DEAD Tube Prologue")]
    public void CleanSpecialTitleTest(string input, string expected)
    {
        Assert.Equal(expected, CleanSpecialTitle(input));
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
    [InlineData("Beastars - SP01", 1)]
    [InlineData("Beastars SP01", 1)]
    [InlineData("Beastars Special 01", 0)]
    [InlineData("Beastars Extra 01", 0)]
    [InlineData("Batman Beyond - Return of the Joker (2001) SP01", 1)]
    [InlineData("Batman Beyond - Return of the Joker (2001)", 0)]
    public void ParseSpecialIndexTest(string input, int expected)
    {
        Assert.Equal(expected,  ParseSpecialIndex(input));
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
    [InlineData("Batman - Detective Comics - Rebirth Deluxe Edition Book 04 (2019) (digital) (Son of Ultron-Empire)",
        true, "Batman - Detective Comics - Rebirth Deluxe Edition Book 04")]
    [InlineData("Something - Full Color Edition", false, "Something - Full Color Edition")]
    [InlineData("Witchblade 089 (2005) (Bittertek-DCP) (Top Cow (Image Comics))", true, "Witchblade 089")]
    [InlineData("(C99) Kami-sama Hiroimashita. (SSSS.GRIDMAN)", false, "Kami-sama Hiroimashita.")]
    [InlineData("Dr. Ramune - Mysterious Disease Specialist v01 (2020) (Digital) (danke-Empire)", false, "Dr. Ramune - Mysterious Disease Specialist v01")]
    [InlineData("Magic Knight Rayearth {Omnibus Edition}", false, "Magic Knight Rayearth {}")]
    [InlineData("Magic Knight Rayearth {Omnibus Version}", false, "Magic Knight Rayearth { Version}")]
    public void CleanTitleTest(string input, bool isComic, string expected)
    {
        Assert.Equal(expected, CleanTitle(input, isComic));
    }

    [Theory]
    [InlineData("src: url(fonts/AvenirNext-UltraLight.ttf)", true)]
    [InlineData("src: url(ideal-sans-serif.woff)", true)]
    [InlineData("src: local(\"Helvetica Neue Bold\")", true)]
    [InlineData("src: url(\"/fonts/OpenSans-Regular-webfont.woff2\")", true)]
    [InlineData("src: local(\"/fonts/OpenSans-Regular-webfont.woff2\")", true)]
    [InlineData("src: url(data:application/x-font-woff", false)]
    public void FontCssRewriteMatches(string input, bool expectedMatch)
    {
        Assert.Equal(expectedMatch, FontSrcUrlRegex.Matches(input).Count > 0);
    }

    [Theory]
    [InlineData("src: url(fonts/AvenirNext-UltraLight.ttf)", new [] {"src: url(", "fonts/AvenirNext-UltraLight.ttf", ")"})]
    [InlineData("src: url(ideal-sans-serif.woff)", new [] {"src: url(", "ideal-sans-serif.woff", ")"})]
    [InlineData("src: local(\"Helvetica Neue Bold\")", new [] {"src: local(\"", "Helvetica Neue Bold", "\")"})]
    [InlineData("src: url(\"/fonts/OpenSans-Regular-webfont.woff2\")", new [] {"src: url(\"", "/fonts/OpenSans-Regular-webfont.woff2", "\")"})]
    [InlineData("src: local(\"/fonts/OpenSans-Regular-webfont.woff2\")", new [] {"src: local(\"", "/fonts/OpenSans-Regular-webfont.woff2", "\")"})]
    public void FontCssCorrectlySeparates(string input, string[] expected)
    {
        Assert.Equal(expected, FontSrcUrlRegex.Match(input).Groups.Values.Select(g => g.Value).Where((_, i) => i > 0).ToArray());
    }


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
    [InlineData("3.5", 3.5)]
    [InlineData("3.5-4.0", 3.5)]
    [InlineData("asdfasdf", 0.0)]
    [InlineData("-10", -10.0)]
    public void MinimumNumberFromRangeTest(string input, float expected)
    {
        Assert.Equal(expected, MinNumberFromRange(input));
    }

    [Theory]
    [InlineData("12-14", 14)]
    [InlineData("24", 24)]
    [InlineData("18-04", 18)]
    [InlineData("18-04.5", 18)]
    [InlineData("40", 40)]
    [InlineData("40a-040b", 0)]
    [InlineData("40.1_a", 0)]
    [InlineData("3.5", 3.5)]
    [InlineData("3.5-4.0", 4.0)]
    [InlineData("asdfasdf", 0.0)]
    [InlineData("-10", -10.0)]
    public void MaximumNumberFromRangeTest(string input, float expected)
    {
        Assert.Equal(expected, MaxNumberFromRange(input));
    }

    [Theory]
    [InlineData("Darker Than Black", "darkerthanblack")]
    [InlineData("Darker Than Black - Something", "darkerthanblacksomething")]
    [InlineData("Darker Than_Black", "darkerthanblack")]
    [InlineData("Citrus", "citrus")]
    [InlineData("Citrus+", "citrus+")]
    [InlineData("Again", "again")]
    [InlineData("카비타", "카비타")]
    [InlineData("06", "06")]
    [InlineData("", "")]
    [InlineData("不安の種+", "不安の種+")]
    public void NormalizeTest(string input, string expected)
    {
        Assert.Equal(expected, Normalize(input));
    }



    [Theory]
    [InlineData("test.jpg", true)]
    [InlineData("test.jpeg", true)]
    [InlineData("test.png", true)]
    [InlineData(".test.jpg", false)]
    [InlineData("!test.jpg", true)]
    [InlineData("test.webp", true)]
    [InlineData("test.gif", true)]
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
    [InlineData("ch1/backcover.png", false)]
    [InlineData("backcover.png", false)]
    [InlineData("back_cover.png", false)]
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
    [InlineData("@recycle/Love Hina/", true)]
    [InlineData("E:/Test/__MACOSX/Love Hina/", true)]
    [InlineData("E:/Test/.caltrash/Love Hina/", true)]
    [InlineData("E:/Test/.yacreaderlibrary/Love Hina/", true)]
    public void HasBlacklistedFolderInPathTest(string inputPath, bool expected)
    {
        Assert.Equal(expected, HasBlacklistedFolderInPath(inputPath));
    }

    [Theory]
    [InlineData("/manga/1/1/1", "/manga/1/1/1")]
    [InlineData("/manga/1/1/1.jpg", "/manga/1/1/1.jpg")]
    [InlineData(@"/manga/1/1\1.jpg", @"/manga/1/1/1.jpg")]
    [InlineData("/manga/1/1//1", "/manga/1/1/1")]
    [InlineData("/manga/1\\1\\1", "/manga/1/1/1")]
    [InlineData("C:/manga/1\\1\\1.jpg", "C:/manga/1/1/1.jpg")]
    public void NormalizePathTest(string inputPath, string expected)
    {
        Assert.Equal(expected, NormalizePath(inputPath));
    }

    [Theory]
    [InlineData("The quick brown fox jumps over the lazy dog")]
    [InlineData("(The quick brown fox jumps over the lazy dog)")]
    [InlineData("()The quick brown fox jumps over the lazy dog")]
    [InlineData("The ()quick brown fox jumps over the lazy dog")]
    [InlineData("The (quick (brown)) fox jumps over the lazy dog")]
    [InlineData("The (quick (brown) fox jumps over the lazy dog)")]
    public void BalancedParenTest_Matches(string input)
    {
        Assert.Matches($@"^{BalancedParen}$", input);
    }

    [Theory]
    [InlineData("(The quick brown fox jumps over the lazy dog")]
    [InlineData("The quick brown fox jumps over the lazy dog)")]
    [InlineData("The )(quick brown fox jumps over the lazy dog")]
    [InlineData("The quick (brown)) fox jumps over the lazy dog")]
    [InlineData("The quick (brown) fox jumps over the lazy dog)")]
    [InlineData("(The ))(quick (brown) fox jumps over the lazy dog")]
    public void BalancedParenTest_DoesNotMatch(string input)
    {
        Assert.DoesNotMatch($@"^{BalancedParen}$", input);
    }

    [Theory]
    [InlineData("The quick brown fox jumps over the lazy dog")]
    [InlineData("[The quick brown fox jumps over the lazy dog]")]
    [InlineData("[]The quick brown fox jumps over the lazy dog")]
    [InlineData("The []quick brown fox jumps over the lazy dog")]
    [InlineData("The [quick [brown]] fox jumps over the lazy dog")]
    [InlineData("The [quick [brown] fox jumps over the lazy dog]")]
    public void BalancedBracketTest_Matches(string input)
    {
        Assert.Matches($@"^{BalancedBracket}$", input);
    }

    [Theory]
    [InlineData("[The quick brown fox jumps over the lazy dog")]
    [InlineData("The quick brown fox jumps over the lazy dog]")]
    [InlineData("The ][quick brown fox jumps over the lazy dog")]
    [InlineData("The quick [brown]] fox jumps over the lazy dog")]
    [InlineData("The quick [brown] fox jumps over the lazy dog]")]
    [InlineData("[The ]][quick [brown] fox jumps over the lazy dog")]
    public void BalancedBracketTest_DoesNotMatch(string input)
    {
        Assert.DoesNotMatch($@"^{BalancedBracket}$", input);
    }
}
