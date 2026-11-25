using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
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
    public void GroupPages_ChapterNumbersInFileNames_GroupsByChapter()
    {
        MangaFileDto[] files = [Chapter(1, "001", 5), Chapter(2, "002", 4), Chapter(3, "003", 6)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
            new EpubTocGroup(9, 14, EpubTocGroupKind.Chapter, "3"),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_FilesOutOfIdOrder_FollowsIdOrder()
    {
        // The cache extracts each file into a folder named by its id, so pages follow id order
        MangaFileDto[] files = [Chapter(12, "003", 6), Chapter(7, "001", 5), Chapter(9, "002", 4)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
            new EpubTocGroup(9, 14, EpubTocGroupKind.Chapter, "3"),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_UnparsedFirstFile_JoinsFirstChapter()
    {
        MangaFileDto[] files = [File(1, "Akame ga KILL! ZERO Extras", 1), Chapter(2, "001", 4), Chapter(3, "002", 4)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_UnparsedMiddleFile_JoinsPreviousChapter()
    {
        MangaFileDto[] files = [Chapter(1, "001", 4), File(2, "Akame ga KILL! ZERO Extras", 1), Chapter(3, "002", 4)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_SameChapterAcrossFiles_OneGroup()
    {
        MangaFileDto[] files = [Chapter(1, "001", 4), Chapter(2, "001", 3), Chapter(3, "002", 4)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 6, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(7, 10, EpubTocGroupKind.Chapter, "2"),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_DecimalChapter_KeptBetweenWholeChapters()
    {
        MangaFileDto[] files = [Chapter(1, "010", 4), Chapter(2, "010.5", 4), Chapter(3, "011", 4)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 3, EpubTocGroupKind.Chapter, "10"),
            new EpubTocGroup(4, 7, EpubTocGroupKind.Chapter, "10.5"),
            new EpubTocGroup(8, 11, EpubTocGroupKind.Chapter, "11"),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_ChapterNumbersGoBackwards_FallsBackToPageRuns()
    {
        MangaFileDto[] files = [Chapter(1, "002", 16), Chapter(2, "001", 16)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 19, EpubTocGroupKind.Pages, null),
            new EpubTocGroup(20, 31, EpubTocGroupKind.Pages, null),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_NoChapterNumbers_FallsBackToPageRuns()
    {
        MangaFileDto[] files = [File(1, "Akame ga KILL! ZERO Extras", 20), File(2, "Akame ga KILL! ZERO Bonus", 20)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 19, EpubTocGroupKind.Pages, null),
            new EpubTocGroup(20, 39, EpubTocGroupKind.Pages, null),
        ], Group(files));
    }

    [Fact]
    public void GroupPages_FilePagesDisagreeWithCache_FallsBackToPageRuns()
    {
        // A stale MangaFile.Pages would shift every later group onto the wrong pages
        MangaFileDto[] files = [Chapter(1, "001", 20), Chapter(2, "002", 19)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 19, EpubTocGroupKind.Pages, null),
            new EpubTocGroup(20, 39, EpubTocGroupKind.Pages, null),
        ], KoboEpubHelper.GroupPages(files, 40, LibraryType.Manga));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void GroupPages_OneFileUpToThirtyPages_OneEntryPerPage(int count)
    {
        MangaFileDto[] files = [Chapter(1, "001", count)];

        var expected = Enumerable.Range(0, count).Select(p => new EpubTocGroup(p, p, EpubTocGroupKind.Page, null));
        Assert.Equal(expected, Group(files));
    }

    [Fact]
    public void GroupPages_ShortChapterWithChapterGroups_ChaptersWin()
    {
        MangaFileDto[] files = [Chapter(1, "001", 5), Chapter(2, "002", 4)];

        Assert.Equal(
        [
            new EpubTocGroup(0, 4, EpubTocGroupKind.Chapter, "1"),
            new EpubTocGroup(5, 8, EpubTocGroupKind.Chapter, "2"),
        ], Group(files));
    }

    [Theory]
    [InlineData(31, new[] { 0, 19, 20, 30 })]
    [InlineData(40, new[] { 0, 19, 20, 39 })]
    [InlineData(45, new[] { 0, 19, 20, 39, 40, 44 })]
    public void GroupPages_OneFile_RunsOfTwenty(int count, int[] bounds)
    {
        MangaFileDto[] files = [Chapter(1, "001", count)];

        var expected = bounds.Chunk(2).Select(b => new EpubTocGroup(b[0], b[1], EpubTocGroupKind.Pages, null));
        Assert.Equal(expected, Group(files));
    }

    [Fact]
    public void GroupPages_NoPages_ReturnsEmpty()
    {
        Assert.Empty(KoboEpubHelper.GroupPages([], 0, LibraryType.Manga));
    }

    private static IList<EpubTocGroup> Group(MangaFileDto[] files) =>
        KoboEpubHelper.GroupPages(files, files.Sum(f => f.Pages), LibraryType.Manga);

    private static MangaFileDto Chapter(int id, string chapter, int pages) =>
        File(id, $"Akame ga KILL! ZERO - c{chapter} (v01) [Digital] [LuCaZ]", pages);

    private static MangaFileDto File(int id, string name, int pages) => new()
    {
        Id = id,
        FilePath = $"/manga/Akame ga KILL! ZERO/{name}.cbz",
        Pages = pages,
        Format = MangaFormat.Archive,
    };

    #endregion
}
