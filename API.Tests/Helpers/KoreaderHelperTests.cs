using API.DTOs.Progress;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;
#nullable enable

public class KoreaderHelperTests
{

    [Theory]
    [InlineData("/body/DocFragment[11]/body/div/a", 10, null)]
    [InlineData("/body/DocFragment[1]/body/div/p[40]", 0, 40)]
    public void GetEpubPositionDto(string koreaderPosition, int page, int? pNumber)
    {
        var expected = EmptyProgressDto();
        expected.BookScrollId = pNumber.HasValue ? $"//BODY/DIV/P[{pNumber}]" : null;
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
        expected.BookScrollId = pNumber.HasValue ? $"//BODY/DIV/P[{pNumber}]" : null;
        expected.PageNum = page;
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);
        Assert.Equal(expected.BookScrollId?.ToLowerInvariant(), actual.BookScrollId);
        Assert.Equal(expected.PageNum, actual.PageNum);
    }

    [Theory]
    [InlineData("/body/DocFragment[3]/body/h1/text().0", 2, "//body/h1")]
    [InlineData("/body/DocFragment[10].0", 9, null)]
    [InlineData("/body/DocFragment[9]/body/p[52]/text().248", 8, "//body/p[52]")]
    public void GetEpubPositionDto_UserSubmitted(string koreaderPosition, int page, string? convertedXpath)
    {
        var expected = EmptyProgressDto();
        expected.PageNum = page;
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);
        if (!string.IsNullOrEmpty(convertedXpath))
        {
            Assert.Equal(convertedXpath, actual.BookScrollId);
        }
        Assert.Equal(expected.PageNum, actual.PageNum);
    }

    [Theory]
    [InlineData("//body/p[20]", 5, "/body/DocFragment[5]/body/p[20]")]
    [InlineData(null, 10, "/body/DocFragment[10]/body/p[1]")] // I've not seen a null/just an "A" from Koreader in testing
    public void GetKoreaderPosition(string? scrollId, int page, string koreaderPosition)
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
