using Kavita.Common.Helpers;

namespace Kavita.Common.Tests.Helpers;

public class ExternalIdParserTests
{
    [Theory]
    [InlineData("https://anilist.co/manga/35851/Byeontaega-Doeja/", 35851)]
    [InlineData("https://anilist.co/manga/30105", 30105)]
    [InlineData("https://anilist.co/manga/30105/Kekkaishi/", 30105)]
    public void CanParseWeblink_AniList(string link, int? expectedId)
    {
        Assert.Equal(ExternalIdParser.GetAniListId(link), expectedId);
    }

    [Theory]
    [InlineData("https://mangadex.org/title/316d3d09-bb83-49da-9d90-11dc7ce40967/honzuki-no-gekokujou-shisho-ni-naru-tame-ni-wa-shudan-wo-erandeiraremasen-dai-3-bu-ryouchi-ni-hon-o", "316d3d09-bb83-49da-9d90-11dc7ce40967")]
    public void CanParseWeblink_MangaDex(string link, string expectedId)
    {
        Assert.Equal(ExternalIdParser.GetMangaDexId(link), expectedId);
    }

    [Theory]
    [InlineData("https://comicvine.gamespot.com/chew-1-taster-s-choice-part-1-of-5/4000-159233/", "159233")]
    public void CanParseWeblink_ComicVine(string link, string expectedId)
    {
        Assert.Equal(ExternalIdParser.GetComicVineId(link).Item1, expectedId);
    }

    [Theory]
    [InlineData("https://mangabaka.org/3391", 3391)]
    [InlineData("https://mangabaka.org/3391/Some-Slug/", 3391)]
    [InlineData("https://mangabaka.org/3391?q=Something+Something", 3391)]
    [InlineData("https://mangabaka.org/novel/84752/Spice-Wolf", 84752)]
    [InlineData("https://mangabaka.org/manga/10803/Spice-Wolf", 10803)]
    public void CanParseWeblink_MangaBaka(string link, long expectedId)
    {
        Assert.Equal(ExternalIdParser.GetMangaBakaId(link), expectedId);
    }

    [Theory]
    [InlineData("anilist:35851", true, 35851)]
    [InlineData("al:30105", true, 30105)]
    [InlineData("ANILIST:42", true, 42)]
    // A non-matching header must report failure. Regression: a null-check on the parsed value is always
    // true for value types (default(int) == 0 is not null), which made this return true with id 0.
    [InlineData("hardcover:61176", false, 0)]
    [InlineData("mangabaka:3391", false, 0)]
    [InlineData("anilist:notanumber", false, 0)]
    [InlineData("", false, 0)]
    public void TryParseAniListHeader_OnlyMatchesAniList(string text, bool expectedResult, int expectedId)
    {
        var result = ExternalIdParser.TryParseAniListHeader(text, out var id);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("mangabaka:3391", true, 3391)]
    [InlineData("mb:42", true, 42)]
    [InlineData("hardcover:61176", false, 0)]
    [InlineData("anilist:35851", false, 0)]
    public void TryParseMangaBakaHeader_OnlyMatchesMangaBaka(string text, bool expectedResult, long expectedId)
    {
        var result = ExternalIdParser.TryParseMangaBakaHeader(text, out var id);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("hardcover:61176", true, "61176")]
    [InlineData("anilist:35851", false, null)]
    [InlineData("mangabaka:3391", false, null)]
    public void TryParseHardcoverHeader_OnlyMatchesHardcover(string text, bool expectedResult, string? expectedId)
    {
        var result = ExternalIdParser.TryParseHardcoverHeader(text, out var id);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("https://hardcover.app/books/wicked-town-1", "wicked-town-1", true)]
    [InlineData("https://hardcover.app/books/wicked-town-1/", "wicked-town-1", true)]
    [InlineData("https://hardcover.app/books/wicked-town-1/editions/12345", "wicked-town-1", true)]
    // A series url is not standalone, even if the user ticked the toggle
    [InlineData("https://hardcover.app/series/the-expanse", "the-expanse", false)]
    [InlineData("https://HARDCOVER.app/books/wicked-town-1", "wicked-town-1", true)]
    // The numeric-id links Kavita generates itself are not slugs, both current and legacy
    [InlineData("https://hardcover.app/id/series/12345", null, false)]
    [InlineData("https://hardcover.app/id/book/12345", null, false)]
    [InlineData("https://hardcover.app/series/id/12345", null, false)]
    [InlineData("https://hardcover.app/books/id/12345", null, false)]
    [InlineData("https://mangabaka.org/3391", null, false)]
    [InlineData("hardcover:61176", null, false)]
    [InlineData("", null, false)]
    [InlineData(null, null, false)]
    public void GetHardcoverSlugFromUrl_ExtractsSlugAndStandAlone(string? text, string? expectedSlug, bool expectedIsStandAlone)
    {
        var result = ExternalIdParser.GetHardcoverSlugFromUrl(text);

        Assert.Equal(expectedSlug, result?.Slug);
        Assert.Equal(expectedIsStandAlone, result?.IsStandAlone ?? false);
    }
}
