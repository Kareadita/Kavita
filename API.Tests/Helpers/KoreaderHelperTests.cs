using API.DTOs.Progress;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;
#nullable enable

public class KoreaderHelperTests
{
    #region UpdateProgressDto Tests

    [Theory]
    [InlineData("/body/DocFragment[11]/body/div/a", 10, null)] // Anchor tags return null BookScrollId
    [InlineData("/body/DocFragment[1]/body/div/p[40]", 0, "//body/div/p[40]")]
    [InlineData("/body/DocFragment[5]/body/section/div[2]", 4, "//body/section/div[2]")]
    public void UpdateProgressDto_StandardXPath(string koreaderPosition, int expectedPage, string? expectedScrollId)
    {
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);

        Assert.Equal(expectedPage, actual.PageNum);
        Assert.Equal(expectedScrollId, actual.BookScrollId);
    }

    [Theory]
    [InlineData("/body/DocFragment[8]/body/div/p[28]/text().264", 7, "//body/div/p[28]")]
    [InlineData("/body/DocFragment[3]/body/h1/text().0", 2, "//body/h1")]
    [InlineData("/body/DocFragment[9]/body/p[52]/text().248", 8, "//body/p[52]")]
    [InlineData("/body/DocFragment[6]/body/div/span.0", 5, "//body/div/span")] // Trailing .0 stripped
    public void UpdateProgressDto_WithTextOffsets(string koreaderPosition, int expectedPage, string? expectedScrollId)
    {
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);

        Assert.Equal(expectedPage, actual.PageNum);
        Assert.Equal(expectedScrollId, actual.BookScrollId);
    }

    [Theory]
    [InlineData("/body/DocFragment[10].0", 9, null)] // Short path - no scroll ID determinable
    [InlineData("/body/DocFragment[5]", 4, null)]
    [InlineData("/body/DocFragment[1]/body", 0, null)] // Too short for full path extraction
    public void UpdateProgressDto_ShortPaths(string koreaderPosition, int expectedPage, string? expectedScrollId)
    {
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);

        Assert.Equal(expectedPage, actual.PageNum);
        Assert.Equal(expectedScrollId, actual.BookScrollId);
    }

    [Theory]
    [InlineData("#_doc_fragment_10", 9, null)]
    [InlineData("#_doc_fragment_1", 0, null)]
    [InlineData("#_doc_fragment_10_ some_anchor", 9, null)] // With trailing anchor
    [InlineData("#_doc_fragment10", 9, null)] // Legacy format without underscore
    [InlineData("#_doc_fragment1", 0, null)]
    public void UpdateProgressDto_HashFragmentFormat(string koreaderPosition, int expectedPage, string? expectedScrollId)
    {
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);

        Assert.Equal(expectedPage, actual.PageNum);
        Assert.Equal(expectedScrollId, actual.BookScrollId);
    }

    [Theory]
    [InlineData("5", 4)] // Archive/PDF page number (1-indexed from KOReader)
    [InlineData("1", 0)]
    [InlineData("100", 99)]
    public void UpdateProgressDto_NumericOnly(string koreaderPosition, int expectedPage)
    {
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);

        Assert.Equal(expectedPage, actual.PageNum);
    }

    [Theory]
    [InlineData("/body/DocFragment[11]/body/id(\"chapter1\")", 10, null)] // id() selectors not supported
    [InlineData("/body/DocFragment[5]/body/div/id(\"section2\")/p[1]", 4, null)]
    public void UpdateProgressDto_IdSelectors(string koreaderPosition, int expectedPage, string? expectedScrollId)
    {
        var actual = EmptyProgressDto();

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition);

        Assert.Equal(expectedPage, actual.PageNum);
        Assert.Equal(expectedScrollId, actual.BookScrollId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateProgressDto_EmptyOrNull_NoChanges(string? koreaderPosition)
    {
        var actual = EmptyProgressDto();
        actual.PageNum = 5;
        actual.BookScrollId = "//body/p[1]";

        KoreaderHelper.UpdateProgressDto(actual, koreaderPosition!);

        // Should remain unchanged
        Assert.Equal(5, actual.PageNum);
        Assert.Equal("//body/p[1]", actual.BookScrollId);
    }

    #endregion

    #region GetKoreaderPosition Tests

    [Theory]
    [InlineData("//body/p[20]", 4, "/body/DocFragment[5]/body/p[20].0")]
    [InlineData("//body/div/section[3]", 0, "/body/DocFragment[1]/body/div/section[3].0")]
    [InlineData("//body/h1", 9, "/body/DocFragment[10]/body/h1.0")]
    public void GetKoreaderPosition_WithScrollId(string? scrollId, int page, string expectedPosition)
    {
        var given = EmptyProgressDto();
        given.BookScrollId = scrollId;
        given.PageNum = page;

        var result = KoreaderHelper.GetKoreaderPosition(given);

        Assert.Equal(expectedPosition, result, ignoreCase: true);
    }

    [Theory]
    [InlineData(null, 9, "/body/DocFragment[10].0")]
    [InlineData("", 0, "/body/DocFragment[1].0")]
    [InlineData(null, 4, "/body/DocFragment[5].0")]
    public void GetKoreaderPosition_NoScrollId_ReturnsFragmentOnly(string? scrollId, int page, string expectedPosition)
    {
        var given = EmptyProgressDto();
        given.BookScrollId = scrollId;
        given.PageNum = page;

        var result = KoreaderHelper.GetKoreaderPosition(given);

        Assert.Equal(expectedPosition, result, ignoreCase: true);
    }

    [Theory]
    [InlineData("id(\"chapter1\")", 5, "/body/DocFragment[6].0")]
    [InlineData("id(\"h2\")", 10, "/body/DocFragment[11].0")]
    public void GetKoreaderPosition_IdSelector_ReturnsFragmentOnly(string scrollId, int page, string expectedPosition)
    {
        var given = EmptyProgressDto();
        given.BookScrollId = scrollId;
        given.PageNum = page;

        var result = KoreaderHelper.GetKoreaderPosition(given);

        Assert.Equal(expectedPosition, result, ignoreCase: true);
    }

    #endregion

    #region HashContents Tests

    [Theory]
    [InlineData("./Data/AesopsFables.epub", "8795ACA4BF264B57C1EEDF06A0CEE688")]
    public void HashContents_ValidFile_ReturnsExpectedHash(string filePath, string expectedHash)
    {
        Assert.Equal(expectedHash, KoreaderHelper.HashContents(filePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("./Data/NonExistent.epub")]
    public void HashContents_InvalidFile_ReturnsNull(string? filePath)
    {
        Assert.Null(KoreaderHelper.HashContents(filePath!));
    }

    #endregion

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
