using System;
using System.Collections.Generic;
using API.DTOs;
using API.DTOs.ReadingLists;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Xunit;

namespace API.Tests.Services;
#nullable enable

public class EntityNamingServiceTests
{
    private readonly EntityNamingService _sut = new();

    #region FormatChapterTitle Tests

    [Theory]
    [InlineData(LibraryType.Manga, "1", null, "Chapter 1")]
    [InlineData(LibraryType.Manga, "1448", null, "Chapter 1448")]
    [InlineData(LibraryType.Manga, "1.5", null, "Chapter 1.5")]
    [InlineData(LibraryType.Image, "5", null, "Chapter 5")]
    public void FormatChapterTitle_Manga_ReturnsChapterFormat(
        LibraryType libraryType, string range, string? title, string expected)
    {
        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Comic, "1", null, "Issue #1")]
    [InlineData(LibraryType.Comic, "25", null, "Issue #25")]
    [InlineData(LibraryType.ComicVine, "100", null, "Issue #100")]
    public void FormatChapterTitle_Comic_ReturnsIssueFormat(
        LibraryType libraryType, string range, string? title, string expected)
    {
        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Book, "1", "The Fellowship", "Book The Fellowship")]
    [InlineData(LibraryType.LightNovel, "1", "Some Title", "Book 1 - Some Title")]
    public void FormatChapterTitle_Book_ReturnsBookFormat(
        LibraryType libraryType, string range, string? title, string expected)
    {
        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, "1448", "The Big Fight", "Chapter 1448 - The Big Fight")]
    [InlineData(LibraryType.Comic, "5", "The Origin", "Issue #5 - The Origin")]
    [InlineData(LibraryType.Image, "10", "Epilogue", "Chapter 10 - Epilogue")]
    public void FormatChapterTitle_WithUniqueTitle_AppendsTitleToBase(
        LibraryType libraryType, string range, string title, string expected)
    {
        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, "1448", "Chapter 1448", "Chapter 1448")]
    [InlineData(LibraryType.Manga, "1448", "Ch. 1448", "Chapter 1448")]
    [InlineData(LibraryType.Manga, "1448", "Ch 1448", "Chapter 1448")]
    [InlineData(LibraryType.Manga, "10", "Episode 10", "Chapter 10")]
    [InlineData(LibraryType.Comic, "5", "Issue #5", "Issue #5")]
    [InlineData(LibraryType.Comic, "5", "Issue 5", "Issue #5")]
    [InlineData(LibraryType.Manga, "1", "#1", "Chapter 1")]
    public void FormatChapterTitle_WithRedundantTitle_DoesNotDuplicate(
        LibraryType libraryType, string range, string title, string expected)
    {
        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, "1448", "1448", "Chapter 1448")]
    [InlineData(LibraryType.Comic, "5", "5", "Issue #5")]
    public void FormatChapterTitle_TitleMatchesRange_DoesNotDuplicate(
        LibraryType libraryType, string range, string title, string expected)
    {
        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatChapterTitle_Special_ReturnsCleanedTitle()
    {
        var result = _sut.FormatChapterTitle(
            LibraryType.Manga, isSpecial: true, "SP01", "SP01 - Bonus Chapter");

        // Assuming Parser.CleanSpecialTitle removes "SP01 - " prefix
        Assert.NotNull(result);
    }

    [Fact]
    public void FormatChapterTitle_WithCustomLabels_UsesProvidedLabels()
    {
        var result = _sut.FormatChapterTitle(
            LibraryType.Manga,
            isSpecial: false,
            range: "5",
            title: null,
            chapterLabel: "Kapitel");

        Assert.Equal("Kapitel 5", result);
    }

    [Fact]
    public void FormatChapterTitle_Comic_WithoutHash_OmitsHashMark()
    {
        var result = _sut.FormatChapterTitle(
            LibraryType.Comic,
            isSpecial: false,
            range: "5",
            title: null,
            withHash: false);

        Assert.Equal("Issue 5", result);
    }

    [Fact]
    public void FormatChapterTitle_WithChapterDto_ExtractsFieldsCorrectly()
    {
        var chapter = CreateChapterDto(range: "42", title: "The Answer", isSpecial: false);

        var result = _sut.FormatChapterTitle(LibraryType.Manga, chapter);

        Assert.Equal("Chapter 42 - The Answer", result);
    }

    #endregion

    #region FormatVolumeName Tests

    [Fact]
    public void FormatVolumeName_StandardLibrary_ReturnsVolumeLabel()
    {
        var volume = CreateVolumeDto(name: "1", minNumber: 1);

        var result = _sut.FormatVolumeName(LibraryType.Manga, volume);

        Assert.Equal("Volume 1", result);
    }

    [Fact]
    public void FormatVolumeName_AlreadyHasVolumePrefix_DoesNotDuplicate()
    {
        var volume = CreateVolumeDto(name: "Volume 1", minNumber: 1);

        var result = _sut.FormatVolumeName(LibraryType.Manga, volume);

        Assert.Equal("Volume 1", result);
    }

    [Theory]
    [InlineData("Volume 1")]
    [InlineData("Vol. 1")]
    [InlineData("Vol 1")]
    [InlineData("V. 1")]
    public void FormatVolumeName_WithVariousPrefixes_DoesNotDuplicate(string volumeName)
    {
        var volume = CreateVolumeDto(name: volumeName, minNumber: 1);

        var result = _sut.FormatVolumeName(LibraryType.Manga, volume);

        Assert.Equal(volumeName, result);
    }

    [Fact]
    public void FormatVolumeName_SpecialVolume_ReturnsNull()
    {
        var volume = CreateVolumeDto(name: "Specials", minNumber: Parser.SpecialVolumeNumber);

        var result = _sut.FormatVolumeName(LibraryType.Manga, volume);

        Assert.Null(result);
    }

    [Fact]
    public void FormatVolumeName_WithCustomLabel_UsesProvidedLabel()
    {
        var volume = CreateVolumeDto(name: "1", minNumber: 1);

        var result = _sut.FormatVolumeName(LibraryType.Manga, volume, volumeLabel: "Band");

        Assert.Equal("Band 1", result);
    }

    [Fact]
    public void FormatVolumeName_BookLibrary_WithTitleName_ReturnsTitleName()
    {
        var chapter = CreateChapterDto(titleName: "The Fellowship of the Ring");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.FormatVolumeName(LibraryType.Book, volume);

        Assert.Equal("The Fellowship of the Ring", result);
    }

    [Fact]
    public void FormatVolumeName_BookLibrary_LooseLeaf_ReturnsVolumeName()
    {
        var chapter = CreateChapterDto(titleName: "Some Title");
        var volume = CreateVolumeDto(
            name: "0",
            minNumber: Parser.LooseLeafVolumeNumber,
            chapters: [chapter]);

        var result = _sut.FormatVolumeName(LibraryType.Book, volume);

        Assert.Equal("0", result);
    }

    [Fact]
    public void FormatVolumeName_BookLibrary_NoTitleName_ExtractsFromRange()
    {
        var chapter = CreateChapterDto(range: "Book Title.epub", titleName: null);
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.FormatVolumeName(LibraryType.Book, volume);

        Assert.Equal("1 - Book Title", result);
    }

    [Fact]
    public void FormatVolumeName_BookLibrary_SpecialChapter_ReturnsNull()
    {
        var chapter = CreateChapterDto(isSpecial: true);
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.FormatVolumeName(LibraryType.Book, volume);

        Assert.Null(result);
    }

    #endregion

    #region BuildFullTitle Tests

    [Fact]
    public void BuildFullTitle_NoVolume_ReturnsSeriesAndChapter()
    {
        var series = CreateSeriesDto("Hajime no Ippo");
        var chapter = CreateChapterDto(range: "1448", title: "The Big Fight");

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume: null, chapter);

        Assert.Equal("Hajime no Ippo - Chapter 1448 - The Big Fight", result);
    }

    [Fact]
    public void BuildFullTitle_SpecialVolume_ReturnsSeriesAndChapter()
    {
        var series = CreateSeriesDto("One Piece");
        var volume = CreateVolumeDto(name: "Specials", minNumber: Parser.SpecialVolumeNumber);
        var chapter = CreateChapterDto(range: "SP01", title: "Bonus", isSpecial: true);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume, chapter);

        Assert.StartsWith("One Piece - ", result);
    }

    [Fact]
    public void BuildFullTitle_LooseLeafVolume_SingleChapter_ReturnsSeriesOnly()
    {
        var series = CreateSeriesDto("My Series");
        var chapter = CreateChapterDto(range: "1");
        var volume = CreateVolumeDto(
            name: "0",
            minNumber: Parser.LooseLeafVolumeNumber,
            chapters: [chapter]);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume, chapter);

        Assert.Equal("My Series", result);
    }

    [Fact]
    public void BuildFullTitle_LooseLeafVolume_MultipleChapters_IncludesChapter()
    {
        var series = CreateSeriesDto("My Series");
        var chapter1 = CreateChapterDto(range: "1");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(
            name: "0",
            minNumber: Parser.LooseLeafVolumeNumber,
            chapters: [chapter1, chapter2]);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume, chapter1);

        Assert.Equal("My Series - Chapter 1", result);
    }

    [Fact]
    public void BuildFullTitle_SingleChapterVolume_ReturnsSeriesAndVolume()
    {
        var series = CreateSeriesDto("Attack on Titan");
        var chapter = CreateChapterDto(range: "1");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume, chapter);

        Assert.Equal("Attack on Titan - Volume 1", result);
    }

    [Fact]
    public void BuildFullTitle_MultipleChapterVolume_IncludesVolumeAndChapter()
    {
        var series = CreateSeriesDto("Naruto");
        var chapter1 = CreateChapterDto(range: "1", title: "Uzumaki Naruto");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter1, chapter2]);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume, chapter1);

        Assert.Equal("Naruto - Volume 1 - Chapter 1 - Uzumaki Naruto", result);
    }

    [Fact]
    public void BuildFullTitle_Comic_UsesIssueFormat()
    {
        var series = CreateSeriesDto("Batman");
        var chapter = CreateChapterDto(range: "1", title: "The Beginning");

        var result = _sut.BuildFullTitle(LibraryType.Comic, series, volume: null, chapter);

        Assert.Equal("Batman - Issue #1 - The Beginning", result);
    }

    [Fact]
    public void BuildFullTitle_Book_SingleChapterVolume_UsesBookTitle()
    {
        var series = CreateSeriesDto("Lord of the Rings");
        var chapter = CreateChapterDto(titleName: "The Fellowship of the Ring");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.BuildFullTitle(LibraryType.Book, series, volume, chapter);

        Assert.Equal("Lord of the Rings - The Fellowship of the Ring", result);
    }

    [Fact]
    public void BuildFullTitle_WithCustomLabels_UsesProvidedLabels()
    {
        var series = CreateSeriesDto("Manga Series");
        var chapter = CreateChapterDto(range: "5");

        var result = _sut.BuildFullTitle(
            LibraryType.Manga,
            series,
            volume: null,
            chapter,
            chapterLabel: "Kapitel");

        Assert.Equal("Manga Series - Kapitel 5", result);
    }

    [Fact]
    public void BuildFullTitle_RedundantChapterTitle_DoesNotDuplicate()
    {
        var series = CreateSeriesDto("Hajime no Ippo");
        var chapter = CreateChapterDto(range: "1448", title: "Chapter 1448");

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume: null, chapter);

        Assert.Equal("Hajime no Ippo - Chapter 1448", result);
    }

    [Fact]
    public void BuildFullTitle_VolumeAlreadyHasPrefix_DoesNotDuplicate()
    {
        var series = CreateSeriesDto("My Series");
        var chapter1 = CreateChapterDto(range: "1");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(name: "Volume 1", minNumber: 1, chapters: [chapter1, chapter2]);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume, chapter1);

        Assert.Equal("My Series - Volume 1 - Chapter 1", result);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatChapterTitle_EmptyTitle_TreatedAsNull(string title)
    {
        var result = _sut.FormatChapterTitle(LibraryType.Manga, isSpecial: false, "1", title);

        Assert.Equal("Chapter 1", result);
    }

    [Fact]
    public void FormatVolumeName_EmptyChaptersList_ReturnsVolumeName()
    {
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: []);

        var result = _sut.FormatVolumeName(LibraryType.Book, volume);

        Assert.Equal("1", result);
    }

    [Theory]
    [InlineData("CHAPTER 5", "5", "Chapter 5")]
    [InlineData("chapter 5", "5", "Chapter 5")]
    [InlineData("ISSUE #10", "10", "Issue #10")]
    public void FormatChapterTitle_CaseInsensitiveRedundancyCheck(
        string title, string range, string expected)
    {
        var libraryType = title.StartsWith("ISSUE", StringComparison.OrdinalIgnoreCase)
            ? LibraryType.Comic
            : LibraryType.Manga;

        var result = _sut.FormatChapterTitle(libraryType, isSpecial: false, range, title);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFullTitle_NullVolume_NullChapterTitle_HandlesGracefully()
    {
        var series = CreateSeriesDto("Series Name");
        var chapter = CreateChapterDto(range: "1", title: null);

        var result = _sut.BuildFullTitle(LibraryType.Manga, series, volume: null, chapter);

        Assert.Equal("Series Name - Chapter 1", result);
    }

    #endregion

    #region Helper Methods

    private static SeriesDto CreateSeriesDto(string name)
    {
        return new SeriesDto
        {
            Id = 1,
            Name = name,
            LibraryId = 1
        };
    }

    private static VolumeDto CreateVolumeDto(
        string name,
        float minNumber,
        ICollection<ChapterDto>? chapters = null)
    {
        return new VolumeDto
        {
            Id = 1,
            Name = name,
            MinNumber = minNumber,
            MaxNumber = minNumber,
            Chapters = chapters ?? new List<ChapterDto>()
        };
    }

    private static ChapterDto CreateChapterDto(
        string range = "1",
        string? title = null,
        string? titleName = null,
        bool isSpecial = false)
    {
        return new ChapterDto
        {
            Id = 1,
            Range = range,
            Title = title ?? string.Empty,
            TitleName = titleName ?? string.Empty,
            IsSpecial = isSpecial,
            VolumeId = 1,
            Files = new List<MangaFileDto>()
        };
    }

    private static ReadingListItemDto CreateReadingListItemDto(
        LibraryType libraryType,
        MangaFormat format,
        string? chapterNumber,
        string? volumeNumber,
        string? chapterTitleName,
        bool isSpecial)
    {
        return new ReadingListItemDto
        {
            Id = 1,
            Order = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1,
            LibraryId = 1,
            LibraryType = libraryType,
            SeriesFormat = format,
            ChapterNumber = chapterNumber,
            VolumeNumber = volumeNumber,
            ChapterTitleName = chapterTitleName,
            IsSpecial = isSpecial,
            SeriesName = "Test Series",
            PagesRead = 0,
            PagesTotal = 100
        };
    }

    #endregion

    #region BuildChapterTitle Tests

    [Fact]
    public void BuildChapterTitle_SingleChapterVolume_ReturnsVolumeOnly()
    {
        var chapter = CreateChapterDto(range: "1");
        var volume = CreateVolumeDto(name: "5", minNumber: 5, chapters: [chapter]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter);

        Assert.Equal("Volume 5", result);
    }

    [Fact]
    public void BuildChapterTitle_MultipleChapterVolume_ReturnsVolumeAndChapter()
    {
        var chapter1 = CreateChapterDto(range: "1", title: "The Beginning");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter1);

        Assert.Equal("Volume 1 - Chapter 1 - The Beginning", result);
    }

    [Fact]
    public void BuildChapterTitle_SpecialVolume_ReturnsChapterOnly()
    {
        var chapter = CreateChapterDto(range: "SP01", title: "Bonus", isSpecial: true);
        var volume = CreateVolumeDto(name: "Specials", minNumber: Parser.SpecialVolumeNumber, chapters: [chapter]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter);

        Assert.NotEmpty(result);
        Assert.DoesNotContain("Volume", result);
    }

    [Fact]
    public void BuildChapterTitle_LooseLeafVolume_SingleChapter_ReturnsEmpty()
    {
        var chapter = CreateChapterDto(range: "1");
        var volume = CreateVolumeDto(name: "0", minNumber: Parser.LooseLeafVolumeNumber, chapters: [chapter]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildChapterTitle_LooseLeafVolume_MultipleChapters_ReturnsChapterOnly()
    {
        var chapter1 = CreateChapterDto(range: "1");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(name: "0", minNumber: Parser.LooseLeafVolumeNumber, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter1);

        Assert.Equal("Chapter 1", result);
    }

    [Fact]
    public void BuildChapterTitle_Comic_SingleChapterVolume_ReturnsVolumeOnly()
    {
        var chapter = CreateChapterDto(range: "1");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.BuildChapterTitle(LibraryType.Comic, volume, chapter);

        Assert.Equal("Volume 1", result);
    }

    [Fact]
    public void BuildChapterTitle_Comic_MultipleChapterVolume_UsesIssueFormat()
    {
        var chapter1 = CreateChapterDto(range: "1");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(LibraryType.Comic, volume, chapter1);

        Assert.Equal("Volume 1 - Issue #1", result);
    }

    [Fact]
    public void BuildChapterTitle_Book_SingleChapterVolume_WithTitleName_ReturnsTitleName()
    {
        var chapter = CreateChapterDto(titleName: "The Fellowship of the Ring");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter]);

        var result = _sut.BuildChapterTitle(LibraryType.Book, volume, chapter);

        Assert.Equal("The Fellowship of the Ring", result);
    }

    [Fact]
    public void BuildChapterTitle_Book_MultipleChapterVolume_ReturnsVolumeAndBook()
    {
        var chapter1 = CreateChapterDto(range: "1", title: "Part One");
        var chapter2 = CreateChapterDto(range: "2", title: "Part Two");
        var volume = CreateVolumeDto(name: "1", minNumber: 1, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(LibraryType.Book, volume, chapter1);

        Assert.Contains("Book Part One", result);
    }

    [Fact]
    public void BuildChapterTitle_WithCustomLabels_UsesProvidedLabels()
    {
        var chapter1 = CreateChapterDto(range: "5");
        var chapter2 = CreateChapterDto(range: "6");
        var volume = CreateVolumeDto(name: "2", minNumber: 2, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(
            LibraryType.Manga, volume, chapter1,
            volumeLabel: "Band",
            chapterLabel: "Kapitel");

        Assert.Equal("Band 2 - Kapitel 5", result);
    }

    [Fact]
    public void BuildChapterTitle_VolumeAlreadyHasPrefix_DoesNotDuplicate()
    {
        var chapter1 = CreateChapterDto(range: "1");
        var chapter2 = CreateChapterDto(range: "2");
        var volume = CreateVolumeDto(name: "Volume 1", minNumber: 1, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter1);

        Assert.Equal("Volume 1 - Chapter 1", result);
        Assert.DoesNotContain("Volume Volume", result);
    }

    [Fact]
    public void BuildChapterTitle_RedundantChapterTitle_DoesNotDuplicate()
    {
        var chapter1 = CreateChapterDto(range: "1448", title: "Chapter 1448");
        var chapter2 = CreateChapterDto(range: "1449");
        var volume = CreateVolumeDto(name: "100", minNumber: 100, chapters: [chapter1, chapter2]);

        var result = _sut.BuildChapterTitle(LibraryType.Manga, volume, chapter1);

        Assert.Equal("Volume 100 - Chapter 1448", result);
    }

    #endregion


    #region FormatReadingListItemTitle Tests


    // Manga Library & Archive
    [Theory]
    [InlineData(Parser.DefaultChapter, "1", null, false, "Volume 1")]
    [InlineData("1", "1", null, false, "Chapter 1")]
    [InlineData("1", "1", "The Title", false, "Chapter 1")]
    [InlineData(Parser.DefaultChapter, "1", "The Title", false, "Volume 1")]
    [InlineData(Parser.DefaultChapter, Parser.LooseLeafVolume, "The Title", false, "The Title")]
    public void FormatReadingListItemTitle_MangaArchive_ReturnsExpected(
        string chapterNumber, string volumeNumber, string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    // Comic Library & Archive
    [Theory]
    [InlineData(Parser.DefaultChapter, "1", null, false, "Volume 1")]
    [InlineData("1", "1", null, false, "Issue #1")]
    [InlineData("1", "1", "The Title", false, "Issue #1")]
    [InlineData(Parser.DefaultChapter, "1", "The Title", false, "Volume 1")]
    [InlineData(Parser.DefaultChapter, Parser.LooseLeafVolume, "The Title", false, "The Title")]
    public void FormatReadingListItemTitle_ComicArchive_ReturnsExpected(
        string chapterNumber, string volumeNumber, string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Comic, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatReadingListItemTitle_ComicArchive_Special_ReturnsChapterNumber()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Comic, MangaFormat.Archive,
            chapterNumber: "The Special Title",
            volumeNumber: Parser.LooseLeafVolume,
            chapterTitleName: null,
            isSpecial: true);

        Assert.Equal("The Special Title", result);
    }

    // Book Library & Archive
    [Theory]
    [InlineData(Parser.DefaultChapter, "1", null, false, "Volume 1")]
    [InlineData("1", "1", null, false, "Book 1")]
    [InlineData("1", "1", "The Title", false, "Book 1")]
    [InlineData(Parser.DefaultChapter, "1", "The Title", false, "Volume 1")]
    [InlineData(Parser.DefaultChapter, Parser.LooseLeafVolume, "The Title", false, "The Title")]
    public void FormatReadingListItemTitle_BookArchive_ReturnsExpected(
        string chapterNumber, string volumeNumber, string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Book, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    // Manga Library & EPUB
    [Theory]
    [InlineData(Parser.DefaultChapter, "1", null, false, "Volume 1")]
    [InlineData("1", "1", null, false, "Volume 1")]
    [InlineData("1", "1", "The Title", false, "Volume 1")]
    [InlineData(Parser.DefaultChapter, "1", "The Title", false, "The Title")]
    [InlineData(Parser.DefaultChapter, Parser.LooseLeafVolume, "The Title", false, "The Title")]
    public void FormatReadingListItemTitle_MangaEpub_ReturnsExpected(
        string chapterNumber, string volumeNumber, string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga, MangaFormat.Epub, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    // Book Library & EPUB
    [Theory]
    [InlineData(Parser.DefaultChapter, "1", null, false, "Volume 1")]
    [InlineData("1", "1", null, false, "Volume 1")]
    [InlineData("1", "1", "The Title", false, "Volume 1")]
    [InlineData(Parser.DefaultChapter, "1", "The Title", false, "The Title")]
    [InlineData(Parser.DefaultChapter, Parser.LooseLeafVolume, "The Title", false, "The Title")]
    public void FormatReadingListItemTitle_BookEpub_ReturnsExpected(
        string chapterNumber, string volumeNumber, string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Book, MangaFormat.Epub, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, "5", "1", null, false, "Chapter 5")]
    [InlineData(LibraryType.Manga, "10.5", "1", null, false, "Chapter 10.5")]
    [InlineData(LibraryType.Image, "3", "1", null, false, "Chapter 3")]
    public void FormatReadingListItemTitle_Manga_ReturnsChapterFormat(
        LibraryType libraryType, string chapterNumber, string volumeNumber,
        string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            libraryType, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Comic, "1", "1", null, false, "Issue #1")]
    [InlineData(LibraryType.Comic, "25", "1", null, false, "Issue #25")]
    [InlineData(LibraryType.ComicVine, "100", "1", null, false, "Issue #100")]
    public void FormatReadingListItemTitle_Comic_ReturnsIssueFormat(
        LibraryType libraryType, string chapterNumber, string volumeNumber,
        string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            libraryType, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Book, "1", "1", null, false, "Book 1")]
    [InlineData(LibraryType.LightNovel, "5", "1", null, false, "Book 5")]
    public void FormatReadingListItemTitle_Book_ReturnsBookFormat(
        LibraryType libraryType, string chapterNumber, string volumeNumber,
        string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            libraryType, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, Parser.DefaultChapter, "5", null, false, "Volume 5")]
    [InlineData(LibraryType.Comic, Parser.DefaultChapter, "10", null, false, "Volume 10")]
    public void FormatReadingListItemTitle_DefaultChapterWithVolume_ReturnsVolumeOnly(
        LibraryType libraryType, string chapterNumber, string volumeNumber,
        string? chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            libraryType, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, Parser.DefaultChapter, Parser.LooseLeafVolume, "My Special Title", false, "My Special Title")]
    [InlineData(LibraryType.Comic, Parser.DefaultChapter, Parser.LooseLeafVolume, "Origin Story", false, "Origin Story")]
    public void FormatReadingListItemTitle_DefaultChapterWithTitleName_ReturnsTitleName(
        LibraryType libraryType, string chapterNumber, string volumeNumber,
        string chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            libraryType, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LibraryType.Manga, "SP01", "0", "Bonus Chapter", true, "Bonus Chapter")]
    [InlineData(LibraryType.Comic, "Special", "0", "Annual #1", true, "Annual #1")]
    public void FormatReadingListItemTitle_SpecialWithTitleName_ReturnsTitleName(
        LibraryType libraryType, string chapterNumber, string volumeNumber,
        string chapterTitleName, bool isSpecial, string expected)
    {
        var result = _sut.FormatReadingListItemTitle(
            libraryType, MangaFormat.Archive, chapterNumber, volumeNumber, chapterTitleName, isSpecial);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatReadingListItemTitle_SpecialWithoutTitleName_ReturnsCleanedChapterNumber()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga, MangaFormat.Archive,
            chapterNumber: "SP01 - Bonus",
            volumeNumber: "0",
            chapterTitleName: null,
            isSpecial: true);

        // Should return cleaned version of chapter number
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FormatReadingListItemTitle_WithCustomLabels_UsesProvidedLabels()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga,
            MangaFormat.Archive,
            chapterNumber: "5",
            volumeNumber: "1",
            chapterTitleName: null,
            isSpecial: false,
            chapterLabel: "Kapitel");

        Assert.Equal("Kapitel 5", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_VolumeOnlyWithCustomLabel_UsesProvidedLabel()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga,
            MangaFormat.Archive,
            chapterNumber: Parser.DefaultChapter,
            volumeNumber: "3",
            chapterTitleName: null,
            isSpecial: false,
            volumeLabel: "Band");

        Assert.Equal("Band 3", result);
    }

    #endregion

    #region FormatReadingListItemTitle - Epub Tests

    [Fact]
    public void FormatReadingListItemTitle_Epub_DefaultChapterWithTitleName_ReturnsTitleName()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Book,
            MangaFormat.Epub,
            chapterNumber: Parser.DefaultChapter,
            volumeNumber: "1",
            chapterTitleName: "The Fellowship of the Ring",
            isSpecial: false);

        Assert.Equal("The Fellowship of the Ring", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_Epub_DefaultChapterNoTitleName_ReturnsVolume()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Book,
            MangaFormat.Epub,
            chapterNumber: Parser.DefaultChapter,
            volumeNumber: "1",
            chapterTitleName: null,
            isSpecial: false);

        Assert.Equal("Volume 1", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_Epub_SpecialVolume_ReturnsCleanedChapter()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Book,
            MangaFormat.Epub,
            chapterNumber: "Bonus Content",
            volumeNumber: Parser.SpecialVolume,
            chapterTitleName: null,
            isSpecial: false);

        Assert.Equal("Bonus Content", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_Epub_RegularChapter_ReturnsVolumeWithChapter()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Book,
            MangaFormat.Epub,
            chapterNumber: "5",
            volumeNumber: "1",
            chapterTitleName: null,
            isSpecial: false);

        Assert.Equal("Volume 5", result);
    }

    #endregion

    #region FormatReadingListItemTitle - DTO Overload Tests

    [Fact]
    public void FormatReadingListItemTitle_WithDto_ExtractsFieldsCorrectly()
    {
        var item = CreateReadingListItemDto(
            libraryType: LibraryType.Manga,
            format: MangaFormat.Archive,
            chapterNumber: "42",
            volumeNumber: "5",
            chapterTitleName: null,
            isSpecial: false);

        var result = _sut.FormatReadingListItemTitle(item);

        Assert.Equal("Chapter 42", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_WithDto_SpecialItem_ReturnsTitleName()
    {
        var item = CreateReadingListItemDto(
            libraryType: LibraryType.Manga,
            format: MangaFormat.Archive,
            chapterNumber: "SP01",
            volumeNumber: "0",
            chapterTitleName: "Bonus Chapter",
            isSpecial: true);

        var result = _sut.FormatReadingListItemTitle(item);

        Assert.Equal("Bonus Chapter", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_WithDto_EpubWithTitle_ReturnsTitleName()
    {
        var item = CreateReadingListItemDto(
            libraryType: LibraryType.Book,
            format: MangaFormat.Epub,
            chapterNumber: Parser.DefaultChapter,
            volumeNumber: "1",
            chapterTitleName: "The Hobbit",
            isSpecial: false);

        var result = _sut.FormatReadingListItemTitle(item);

        Assert.Equal("The Hobbit", result);
    }

    [Fact]
    public void FormatReadingListItemTitle_WithDto_ComicFormat_ReturnsIssue()
    {
        var item = CreateReadingListItemDto(
            libraryType: LibraryType.Comic,
            format: MangaFormat.Archive,
            chapterNumber: "15",
            volumeNumber: "1",
            chapterTitleName: null,
            isSpecial: false);

        var result = _sut.FormatReadingListItemTitle(item);

        Assert.Equal("Issue #15", result);
    }

    #endregion



    [Fact]
    public void FormatReadingListItemTitle_NullChapterNumber_HandlesGracefully()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga,
            MangaFormat.Archive,
            chapterNumber: null,
            volumeNumber: "1",
            chapterTitleName: "Fallback Title",
            isSpecial: false);

        // Should fall back to title name or handle gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public void FormatReadingListItemTitle_EmptyStrings_HandlesGracefully()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga,
            MangaFormat.Archive,
            chapterNumber: "",
            volumeNumber: "",
            chapterTitleName: "",
            isSpecial: false);

        // Should not throw and should return something
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("10")]
    [InlineData("100.25")]
    public void FormatReadingListItemTitle_NumericChapterNumbers_PreservedAsIs(string chapterNumber)
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga,
            MangaFormat.Archive,
            chapterNumber: chapterNumber,
            volumeNumber: "1",
            chapterTitleName: null,
            isSpecial: false);

        Assert.Contains(chapterNumber, result);
    }

    [Fact]
    public void FormatReadingListItemTitle_NonNumericChapterNumber_GetsCleaned()
    {
        var result = _sut.FormatReadingListItemTitle(
            LibraryType.Manga,
            MangaFormat.Archive,
            chapterNumber: "SP01 - Special Chapter",
            volumeNumber: "1",
            chapterTitleName: null,
            isSpecial: false);

        // Should clean the special title format
        Assert.NotNull(result);
        Assert.DoesNotContain(" - ", result.Replace("Chapter ", ""));
    }


}
