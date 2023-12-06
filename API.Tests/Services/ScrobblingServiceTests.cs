using API.Services.Plus;
using Xunit;

namespace API.Tests.Services;

public class ScrobblingServiceTests
{
    [Theory]
    [InlineData("https://anilist.co/manga/35851/Byeontaega-Doeja/", 35851)]
    [InlineData("https://anilist.co/manga/30105", 30105)]
    [InlineData("https://anilist.co/manga/30105/Kekkaishi/", 30105)]
    public void CanParseWeblink(string link, long expectedId)
    {
        Assert.Equal(ScrobblingService.ExtractId<long>(link, ScrobblingService.AniListWeblinkWebsite), expectedId);
    }
}
