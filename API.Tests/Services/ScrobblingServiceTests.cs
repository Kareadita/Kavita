using API.Services.Plus;
using Xunit;

namespace API.Tests.Services;
#nullable enable

public class ScrobblingServiceTests
{
    [Theory]
    [InlineData("https://anilist.co/manga/35851/Byeontaega-Doeja/", 35851)]
    [InlineData("https://anilist.co/manga/30105", 30105)]
    [InlineData("https://anilist.co/manga/30105/Kekkaishi/", 30105)]
    public void CanParseWeblink_AniList(string link, int? expectedId)
    {
        Assert.Equal(ScrobblingService.ExtractId<int?>(link, ScrobblingService.AniListWeblinkWebsite), expectedId);
    }

    [Theory]
    [InlineData("https://mangadex.org/title/316d3d09-bb83-49da-9d90-11dc7ce40967/honzuki-no-gekokujou-shisho-ni-naru-tame-ni-wa-shudan-wo-erandeiraremasen-dai-3-bu-ryouchi-ni-hon-o", "316d3d09-bb83-49da-9d90-11dc7ce40967")]
    public void CanParseWeblink_MangaDex(string link, string expectedId)
    {
        Assert.Equal(ScrobblingService.ExtractId<string?>(link, ScrobblingService.MangaDexWeblinkWebsite), expectedId);
    }
}
