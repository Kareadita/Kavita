using Kavita.Services.Helpers.Kobo;
using Xunit;

namespace Kavita.Services.Tests.Helpers;

public class KoboEpubHelperTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(12, 1)]
    [InlineData(12, 2)]
    [InlineData(212, 3)]
    public void GetPath_ThenTryGetPage_ReturnsSamePage(int page, int part)
    {
        var path = KoboEpubHelper.GetPath(page, part);

        Assert.True(KoboEpubHelper.TryGetPage(path, out var parsed));
        Assert.Equal(page, parsed);
    }

    [Theory]
    [InlineData(12, 1, "EPUB/section13.html")]
    [InlineData(12, 2, "EPUB/section13-2.html")]
    public void GetPath_WritesSectionName(int page, int part, string expected)
    {
        Assert.Equal(expected, KoboEpubHelper.GetPath(page, part));
    }

    [Theory]
    [InlineData("EPUB/section13.html", 12)]
    [InlineData("section13.html", 12)]
    [InlineData("OEBPS/section13-2.xhtml", 12)]
    [InlineData("section1", 0)]
    public void TryGetPage_ReadsFileNameOnly(string source, int expected)
    {
        Assert.True(KoboEpubHelper.TryGetPage(source, out var page));
        Assert.Equal(expected, page);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EPUB/page_0013.xhtml")]
    [InlineData("EPUB/section0.html")]
    [InlineData("EPUB/section.html")]
    [InlineData("EPUB/section13-.html")]
    [InlineData("EPUB/section13-x.html")]
    [InlineData("EPUB/section-13.html")]
    [InlineData("EPUB/Section13.html")]
    public void TryGetPage_RejectsNonSectionSource(string? source)
    {
        Assert.False(KoboEpubHelper.TryGetPage(source, out _));
    }
}
