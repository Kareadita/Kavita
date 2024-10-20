using System.IO.Abstractions.TestingHelpers;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Parsers;

public class BasicParserTests
{
    private readonly BasicParser _parser;
    private readonly ILogger<DirectoryService> _dsLogger = Substitute.For<ILogger<DirectoryService>>();
    private const string RootDirectory = "C:/Books/";

    public BasicParserTests()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Books/");
        fileSystem.AddFile("C:/Books/Harry Potter/Harry Potter - Vol 1.epub", new MockFileData(""));

        fileSystem.AddFile("C:/Books/Accel World/Accel World - Volume 1.cbz", new MockFileData(""));
        fileSystem.AddFile("C:/Books/Accel World/Accel World - Volume 1 Chapter 2.cbz", new MockFileData(""));
        fileSystem.AddFile("C:/Books/Accel World/Accel World - Chapter 3.cbz", new MockFileData(""));
        fileSystem.AddFile("C:/Books/Accel World/Accel World Gaiden SP01.cbz", new MockFileData(""));


        fileSystem.AddFile("C:/Books/Accel World/cover.png", new MockFileData(""));

        fileSystem.AddFile("C:/Books/Batman/Batman #1.cbz", new MockFileData(""));

        var ds = new DirectoryService(_dsLogger, fileSystem);
        _parser = new BasicParser(ds, new ImageParser(ds));
    }

    #region Parse_Books



    #endregion

    #region Parse_Manga

    /// <summary>
    /// Tests that when there is a loose leaf cover in the manga library, that it is ignored
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_JustCover_ShouldReturnNull()
    {
        var actual = _parser.Parse(@"C:/Books/Accel World/cover.png", "C:/Books/Accel World/",
            RootDirectory, LibraryType.Manga, null);
        Assert.Null(actual);
    }

    /// <summary>
    /// Tests that when there is a loose leaf cover in the manga library, that it is ignored
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_OtherImage_ShouldReturnNull()
    {
        var actual = _parser.Parse(@"C:/Books/Accel World/page 01.png", "C:/Books/Accel World/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);
    }

    /// <summary>
    /// Tests that when there is a volume and chapter in filename, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_VolumeAndChapterInFilename()
    {
        var actual = _parser.Parse("C:/Books/Mujaki no Rakuen/Mujaki no Rakuen Vol12 ch76.cbz", "C:/Books/Mujaki no Rakuen/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Mujaki no Rakuen", actual.Series);
        Assert.Equal("12", actual.Volumes);
        Assert.Equal("76", actual.Chapters);
        Assert.False(actual.IsSpecial);
    }

    /// <summary>
    /// Tests that when there is a volume in filename, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_JustVolumeInFilename()
    {
        var actual = _parser.Parse("C:/Books/Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen/Vol 1.cbz",
            "C:/Books/Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen", actual.Series);
        Assert.Equal("1", actual.Volumes);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.False(actual.IsSpecial);
    }

    /// <summary>
    /// Tests that when there is a chapter only in filename, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_JustChapterInFilename()
    {
        var actual = _parser.Parse("C:/Books/Beelzebub/Beelzebub_01_[Noodles].zip",
            "C:/Books/Beelzebub/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Beelzebub", actual.Series);
        Assert.Equal(Parser.LooseLeafVolume, actual.Volumes);
        Assert.Equal("1", actual.Chapters);
        Assert.False(actual.IsSpecial);
    }

    /// <summary>
    /// Tests that when there is a SP Marker in filename, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_SpecialMarkerInFilename()
    {
        var actual = _parser.Parse("C:/Books/Summer Time Rendering/Specials/Record 014 (between chapter 083 and ch084) SP11.cbr",
            "C:/Books/Summer Time Rendering/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Summer Time Rendering", actual.Series);
        Assert.Equal(Parser.SpecialVolume, actual.Volumes);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.True(actual.IsSpecial);
    }


    /// <summary>
    /// Tests that when the filename parses as a speical, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_SpecialInFilename()
    {
        var actual = _parser.Parse("C:/Books/Summer Time Rendering/Volume SP01.cbr",
            "C:/Books/Summer Time Rendering/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Summer Time Rendering", actual.Series);
        Assert.Equal("Volume SP01", actual.Title);
        Assert.Equal(Parser.SpecialVolume, actual.Volumes);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.True(actual.IsSpecial);
    }

    /// <summary>
    /// Tests that when the filename parses as a speical, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_SpecialInFilename2()
    {
        var actual = _parser.Parse("M:/Kimi wa Midara na Boku no Joou/Specials/[Renzokusei] Special 1 SP02.zip",
            "M:/Kimi wa Midara na Boku no Joou/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Kimi wa Midara na Boku no Joou", actual.Series);
        Assert.Equal("[Renzokusei] Special 1 SP02", actual.Title);
        Assert.Equal(Parser.SpecialVolume, actual.Volumes);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.True(actual.IsSpecial);
    }

    /// <summary>
    /// Tests that when there is an edition in filename, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaLibrary_EditionInFilename()
    {
        var actual = _parser.Parse("C:/Books/Air Gear/Air Gear Omnibus v01 (2016) (Digital) (Shadowcat-Empire).cbz",
            "C:/Books/Air Gear/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Air Gear", actual.Series);
        Assert.Equal("1", actual.Volumes);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
        Assert.False(actual.IsSpecial);
        Assert.Equal("Omnibus", actual.Edition);
    }

    #endregion

    #region Parse_Books
    /// <summary>
    /// Tests that when there is a volume in filename, it appropriately parses
    /// </summary>
    [Fact]
    public void Parse_MangaBooks_JustVolumeInFilename()
    {
        var actual = _parser.Parse("C:/Books/Epubs/Harrison, Kim - The Good, The Bad, and the Undead - Hollows Vol 2.5.epub",
            "C:/Books/Epubs/",
            RootDirectory, LibraryType.Manga, null);
        Assert.NotNull(actual);

        Assert.Equal("Harrison, Kim - The Good, The Bad, and the Undead - Hollows", actual.Series);
        Assert.Equal("2.5", actual.Volumes);
        Assert.Equal(Parser.DefaultChapter, actual.Chapters);
    }

    #endregion

    #region IsApplicable
    /// <summary>
    /// Tests that this Parser can only be used on images and Image library type
    /// </summary>
    [Fact]
    public void IsApplicable_Fails_WhenNonMatchingLibraryType()
    {
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.Image));
        Assert.False(_parser.IsApplicable("something.cbz", LibraryType.ComicVine));
    }

    /// <summary>
    /// Tests that this Parser can only be used on images and Image library type
    /// </summary>
    [Fact]
    public void IsApplicable_Success_WhenMatchingLibraryType()
    {
        Assert.True(_parser.IsApplicable("something.png", LibraryType.Manga));
        Assert.True(_parser.IsApplicable("something.png", LibraryType.Comic));
        Assert.True(_parser.IsApplicable("something.pdf", LibraryType.Book));
        Assert.True(_parser.IsApplicable("something.epub", LibraryType.LightNovel));
    }


    #endregion
}
