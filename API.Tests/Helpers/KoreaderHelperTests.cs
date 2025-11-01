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
    public void GetEpubPositionDto(string koreaderPosition, int page, int? pNumber)
    {
        var expected = EmptyProgressDto();
        expected.BookScrollId = pNumber.HasValue ? $"//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/DIV/P[{pNumber}]" : null;
        expected.PageNum = page;
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);
        Assert.Equal(expected.BookScrollId?.ToLowerInvariant(), actual.BookScrollId);
        Assert.Equal(expected.PageNum, actual.PageNum);
    }

    [Theory]
    [InlineData("/body/DocFragment[8]/body/div/p[28]/text().264", 7, 28)]
    public void GetEpubPositionDtoWithExtraXpath(string koreaderPosition, int page, int? pNumber)
    {
        var expected = EmptyProgressDto();
        expected.BookScrollId = pNumber.HasValue ? $"//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/DIV/P[{pNumber}]/text().264" : null;
        expected.PageNum = page;
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);
        Assert.Equal(expected.BookScrollId?.ToLowerInvariant(), actual.BookScrollId);
        Assert.Equal(expected.PageNum, actual.PageNum);
    }



    [Theory]
    [InlineData("//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/P[20]", 5, "/body/DocFragment[6]/body/p[20]")]
    [InlineData(null, 10, "/body/DocFragment[11]/body/a")] // I've not seen a null/just an a from Koreader in testing
    public void GetKoreaderPosition(string scrollId, int page, string koreaderPosition)
    {
        var given = EmptyProgressDto();
        given.BookScrollId = scrollId;
        given.PageNum = page;

        Assert.Equal(koreaderPosition.ToUpperInvariant(), KoreaderHelper.GetKoreaderPosition(given).ToUpperInvariant());
    }

    [Theory]
    [InlineData("./Data/AesopsFables.epub", "8795ACA4BF264B57C1EEDF06A0CEE688")]
    public void GetKoreaderHash(string filePath, string hash)
    {
        Assert.Equal(KoreaderHelper.HashContents(filePath), hash);
    }

    private static ProgressDto EmptyProgressDto()
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
