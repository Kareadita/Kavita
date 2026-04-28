using Kavita.Common.Helpers;

namespace Kavita.Common.Tests.Helpers;

public class WeblinkParserTests
{
    [Theory]
    [InlineData("https://anilist.co/manga/35851/Byeontaega-Doeja/", 35851)]
    [InlineData("https://anilist.co/manga/30105", 30105)]
    [InlineData("https://anilist.co/manga/30105/Kekkaishi/", 30105)]
    public void CanParseWeblink_AniList(string link, int? expectedId)
    {
        Assert.Equal(WeblinkParser.GetAniListId(link), expectedId);
    }

    [Theory]
    [InlineData("https://mangadex.org/title/316d3d09-bb83-49da-9d90-11dc7ce40967/honzuki-no-gekokujou-shisho-ni-naru-tame-ni-wa-shudan-wo-erandeiraremasen-dai-3-bu-ryouchi-ni-hon-o", "316d3d09-bb83-49da-9d90-11dc7ce40967")]
    public void CanParseWeblink_MangaDex(string link, string expectedId)
    {
        Assert.Equal(WeblinkParser.GetMangaDexId(link), expectedId);
    }

    [Theory]
    [InlineData("https://comicvine.gamespot.com/chew-1-taster-s-choice-part-1-of-5/4000-159233/", "159233")]
    public void CanParseWeblink_ComicVine(string link, string expectedId)
    {
        Assert.Equal(WeblinkParser.GetComicVineId(link).Item1, expectedId);
    }

    [Theory]
    [InlineData("https://mangabaka.org/3391", 3391)]
    public void CanParseWeblink_MangaBaka(string link, long expectedId)
    {
        Assert.Equal(WeblinkParser.GetMangaBakaId(link), expectedId);
    }
}
