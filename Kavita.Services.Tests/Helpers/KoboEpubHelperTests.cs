using System.Collections.Generic;
using System.Linq;
using Kavita.Models.Kobo;
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

    #region GroupPages

    [Fact]
    public void GroupPages_ChapterNumbersInNames_GroupsByChapter()
    {
        var pages = Pages([..Chapter("001", 5), ..Chapter("002", 4), ..Chapter("003", 6)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
            new EpubTocGroup(9, 14, EpubTocGroupKind.Chapter, "3"),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_UnparsedFirstPage_JoinsFirstChapter()
    {
        var pages = Pages(["cover", ..Chapter("001", 4), ..Chapter("002", 4)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_UnparsedMiddlePage_JoinsPreviousChapter()
    {
        var pages = Pages([..Chapter("001", 4), "credits", ..Chapter("002", 4)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_DecimalChapter_KeptBetweenWholeChapters()
    {
        var pages = Pages([..Chapter("010", 4), ..Chapter("010.5", 4), ..Chapter("011", 4)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 3, EpubTocGroupKind.Chapter, "10"),
            new EpubTocGroup(4, 7, EpubTocGroupKind.Chapter, "10.5"),
            new EpubTocGroup(8, 11, EpubTocGroupKind.Chapter, "11"),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_OneChapterOnly_FallsBackToPageRuns()
    {
        var pages = Pages([..Chapter("001", 45)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 19, EpubTocGroupKind.Pages, null),
            new EpubTocGroup(20, 39, EpubTocGroupKind.Pages, null),
            new EpubTocGroup(40, 44, EpubTocGroupKind.Pages, null),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_EveryPageParsesAsItsOwnChapter_FallsBackToPageRuns()
    {
        // "Page 12" parses as chapter 12, which would give one ToC entry per page
        var pages = Pages(Enumerable.Range(1, 12).Select(p => $"Page {p}").ToArray());

        Assert.Equal([new EpubTocGroup(0, 11, EpubTocGroupKind.Pages, null)], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_ChapterNumbersGoBackwards_FallsBackToPageRuns()
    {
        var pages = Pages([..Chapter("002", 4), ..Chapter("001", 4)]);

        Assert.Equal([new EpubTocGroup(0, 7, EpubTocGroupKind.Pages, null)], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_FlattenedFolders_GroupsByFolder()
    {
        var pages = Pages([..Folder(1, 5), ..Folder(2, 4), ..Folder(3, 3)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Folder, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Folder, "2"),
            new EpubTocGroup(9, 11, EpubTocGroupKind.Folder, "3"),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_OneFolderOnly_FallsBackToPageRuns()
    {
        var pages = Pages([..Folder(1, 25)]);

        Assert.Equal(
        [
            new EpubTocGroup(0, 19, EpubTocGroupKind.Pages, null),
            new EpubTocGroup(20, 24, EpubTocGroupKind.Pages, null),
        ], KoboEpubHelper.GroupPages(pages));
    }

    [Theory]
    [InlineData(15, new[] { 0, 14 })]
    [InlineData(40, new[] { 0, 19, 20, 39 })]
    [InlineData(45, new[] { 0, 19, 20, 39, 40, 44 })]
    public void GroupPages_PlainPageNumbers_RunsOfTwenty(int count, int[] bounds)
    {
        var pages = Pages(Enumerable.Range(1, count).Select(p => $"{p:D3}").ToArray());

        var expected = bounds.Chunk(2).Select(b => new EpubTocGroup(b[0], b[1], EpubTocGroupKind.Pages, null));
        Assert.Equal(expected, KoboEpubHelper.GroupPages(pages));
    }

    [Fact]
    public void GroupPages_NoPages_ReturnsEmpty()
    {
        Assert.Empty(KoboEpubHelper.GroupPages([]));
    }

    private static List<string> Pages(string[] names) => names.Select(n => $"cache/71416/{n}.jpg").ToList();

    private static string[] Chapter(string chapter, int count) => Enumerable.Range(0, count)
        .Select(p => $"Akame ga KILL! ZERO - c{chapter} (v01) - p{p:D3} [Digital] [LuCaZ]")
        .ToArray();

    private static string[] Folder(int folder, int count) => Enumerable.Range(1, count)
        .Select(p => $"{folder:D3}_{p:D4}")
        .ToArray();

    #endregion
}
