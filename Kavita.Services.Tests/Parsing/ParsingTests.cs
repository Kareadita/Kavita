using System.Globalization;
using Kavita.Models.Entities.Enums.Font;
using static Kavita.Services.Scanner.Parser;

namespace Kavita.Services.Tests.Parsing;

public class ParsingTests
{
    [Fact]
    public void ShouldWork()
    {
        var s = 6.5f.ToString(CultureInfo.InvariantCulture);
        var a = float.Parse(s, CultureInfo.InvariantCulture);
        Assert.Equal(6.5f, a);

        s = 6.5f + "";
        a = float.Parse(s, CultureInfo.CurrentCulture);
        Assert.Equal(6.5f, a);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(DefaultChapter, true)]
    public void IsDefaultChapterTest(string input, bool expected)
    {
        Assert.Equal(expected, IsDefaultChapter(input));
    }

    [Fact]
    public void IsDefaultChapterTest_FloatString()
    {
        Assert.True(IsDefaultChapter($"{DefaultChapterNumber}"));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(LooseLeafVolume, true)]
    public void IsLooseLeafTest(string input, bool expected)
    {
        Assert.Equal(expected, IsLooseLeafVolume(input));
    }

    [Fact]
    public void IsLooseLeafTest_FloatString()
    {
        Assert.True(IsLooseLeafVolume($"{LooseLeafVolumeNumber}"));
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
    [InlineData("SP01 1. DEAD Tube Prologue", "1. DEAD Tube Prologue")]
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
    [InlineData("LD Blacklands #1 35 (back cover).png", false)]
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

    [Theory]
    // (완결)
    [InlineData("나에게만 허둥대는 상사의 이야기 04권 (완결)", true)]
    [InlineData("나와 그녀의 하룻밤 영화 03권 [1512x] (완결)", true)]
    [InlineData("데스노트 12권 [1080x] (완결)", true)]
    [InlineData("데스완노트완결 12권 [1080x]", false)]
    // (완)
    [InlineData("불과 달의 노래 276화 (완)", true)]
    [InlineData("부인함락 52권 (완)", true)]
    [InlineData("신을 막아라 (완)", true)]
    // (完)
    [InlineData("러브크래프트 전집 4권(完)", true)]
    [InlineData("서단 결정적 순간 2권 (完)", true)]
    [InlineData("完영업사원 김유빈 150화(完)", true)]
    // [완결]
    [InlineData("토지 17권 [완결]", true)]
    [InlineData("낯선 오후 2권 [완결]", true)]
    [InlineData("낯선 오후 2권 (완결)", true)]
    public void HasEndMarkerTests(string input, bool expected)
    {
        Assert.Equal(expected, HasEndMarker(input));
    }

    #region ParseEpubFontFromFilename

    [Theory]
    // Family is everything before the first '-' descriptor separator
    [InlineData("Family-Regular.ttf", "Family")]
    [InlineData("Family-BoldItalic.ttf", "Family")]
    [InlineData("Family-VariableFont_wght.ttf", "Family")]
    [InlineData("Roboto-Bold.ttf", "Roboto")]
    // camelCase families get split on the lowercase -> uppercase boundary
    [InlineData("OpenSans-Regular.ttf", "Open Sans")]
    [InlineData("PlayfairDisplay-Italic.ttf", "Playfair Display")]
    // No descriptor separator: the whole (camelCase split) name is the family
    [InlineData("Arial.ttf", "Arial")]
    [InlineData("OpenSans.ttf", "Open Sans")]
    public void ParseEpubFontFromFilename_ExtractsFamily(string fileName, string expectedFamily)
    {
        Assert.Equal(expectedFamily, ParseEpubFontFromFilename(fileName).Family);
    }

    [Theory]
    // Style is "italic" when the filename contains "italic" (case-insensitive), else "normal"
    [InlineData("Family-Regular.ttf", "normal")]
    [InlineData("Family-Bold.ttf", "normal")]
    [InlineData("Family-Italic.ttf", "italic")]
    [InlineData("Family-BoldItalic.ttf", "italic")]
    [InlineData("Roboto-boldITALIC.ttf", "italic")]
    public void ParseEpubFontFromFilename_DetectsItalic(string fileName, string expectedStyle)
    {
        Assert.Equal(expectedStyle, ParseEpubFontFromFilename(fileName).Style);
    }

    [Theory]
    // Weights resolve via the longest matching descriptor so specific weights beat substrings
    [InlineData("Family-Thin.ttf", "100")]
    [InlineData("Family-Light.ttf", "300")]
    [InlineData("Family-Regular.ttf", "400")]
    [InlineData("Family-Medium.ttf", "500")]
    [InlineData("Family-SemiBold.ttf", "600")]
    [InlineData("Family-Bold.ttf", "700")]
    [InlineData("Family-ExtraBold.ttf", "800")]
    [InlineData("Family-Black.ttf", "900")]
    // "extrabold" must win over the "bold" substring
    [InlineData("Roboto-ExtraBold.ttf", "800")]
    // Italic descriptor does not affect the resolved weight
    [InlineData("Family-BoldItalic.ttf", "700")]
    // No recognizable descriptor falls back to normal/400
    [InlineData("Arial.ttf", "400")]
    public void ParseEpubFontFromFilename_DetectsWeight(string fileName, string expectedWeight)
    {
        Assert.Equal(expectedWeight, ParseEpubFontFromFilename(fileName).Weight);
    }

    [Theory]
    // Variable fonts map to the full "1 1000" range regardless of casing
    [InlineData("Family-VariableFont_wght.ttf")]
    [InlineData("Inter-VariableFont_slnt,wght.ttf")]
    [InlineData("roboto-variablefont_wght.ttf")]
    public void ParseEpubFontFromFilename_DetectsVariableWeight(string fileName)
    {
        Assert.Equal("1 1000", ParseEpubFontFromFilename(fileName).Weight);
    }

    [Theory]
    // Name strips the variable-axes suffix (everything after '_') and prettifies separators
    [InlineData("Family-Regular.ttf", "Family Regular")]
    [InlineData("Family-BoldItalic.ttf", "Family BoldItalic")]
    [InlineData("Family-VariableFont_wght.ttf", "Family Variable Font")]
    [InlineData("OpenSans-Regular.ttf", "OpenSans Regular")]
    [InlineData("Arial.ttf", "Arial")]
    public void ParseEpubFontFromFilename_BuildsName(string fileName, string expectedName)
    {
        Assert.Equal(expectedName, ParseEpubFontFromFilename(fileName).Name);
    }

    [Fact]
    public void ParseEpubFontFromFilename_PassesThroughFilenameAndProvider()
    {
        var font = ParseEpubFontFromFilename("OpenSans-BoldItalic.ttf");

        Assert.Equal("OpenSans-BoldItalic.ttf", font.FileName);
        Assert.Equal("Open Sans", font.Family);
        Assert.Equal("OpenSans BoldItalic", font.Name);
        Assert.Equal("italic", font.Style);
        Assert.Equal("700", font.Weight);
        Assert.Equal(FontProvider.User, font.Provider);
    }

    #endregion
}
