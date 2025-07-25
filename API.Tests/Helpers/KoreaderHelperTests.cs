using API.DTOs.Koreader;
using API.DTOs.Progress;
using API.Helpers;
using System.Runtime.CompilerServices;
using Xunit;

namespace API.Tests.Helpers;


public class KoreaderHelperTests
{

    [Theory]
    [InlineData("/body/DocFragment[11]/body/div/a", 10, null)]
    [InlineData("/body/DocFragment[1]/body/div/p[40]", 0, 40)]
    [InlineData("/body/DocFragment[8]/body/div/p[28]/text().264", 7, 28)]
    public void GetEpubPositionDto(string koreaderPosition, int page, int? pNumber)
    {
        var expected = EmptyProgressDto();
        expected.BookScrollId = pNumber.HasValue ? $"//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/P[{pNumber}]" : null;
        expected.PageNum = page;
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);
        Assert.Equal(expected.BookScrollId, actual.BookScrollId);
        Assert.Equal(expected.PageNum, actual.PageNum);
    }


    [Theory]
    [InlineData("//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/P[20]", 5, "/body/DocFragment[6]/body/div/p[20]")]
    [InlineData(null, 10, "/body/DocFragment[11]/body/div/a")]
    public void GetKoreaderPosition(string scrollId, int page, string koreaderPosition)
    {
        var given = EmptyProgressDto();
        given.BookScrollId = scrollId;
        given.PageNum = page;

        Assert.Equal(koreaderPosition, KoreaderHelper.GetKoreaderPosition(given));
    }

    [Theory]
    [InlineData("./Data/AesopsFables.epub", "8795ACA4BF264B57C1EEDF06A0CEE688")]
    public void GetKoreaderHash(string filePath, string hash)
    {
        Assert.Equal(KoreaderHelper.HashContents(filePath), hash);
    }

    private ProgressDto EmptyProgressDto()
    {
        return new ProgressDto
        {
            ChapterId = 0,
            PageNum = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0
        };
    }
}
