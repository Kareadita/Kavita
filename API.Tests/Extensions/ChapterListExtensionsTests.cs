using System;
using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;
using Xunit;

namespace API.Tests.Extensions;

public class ChapterListExtensionsTests
{
    private static Chapter CreateChapter(string range, string number, MangaFile file, bool isSpecial)
    {
        return new ChapterBuilder(number, range)
            .WithIsSpecial(isSpecial)
            .WithFile(file)
            .Build();
    }

    private static MangaFile CreateFile(string file, MangaFormat format)
    {
        return new MangaFileBuilder(file, format).Build();
    }

    [Fact]
    public void GetAnyChapterByRange_Test_ShouldBeNull()
    {
        var info = new ParserInfo()
        {
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
            Edition = "",
            Format = MangaFormat.Archive,
            FullFilePath = "/manga/darker than black.cbz",
            Filename = "darker than black.cbz",
            IsSpecial = false,
            Series = "darker than black",
            Title = "darker than black",
            Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume
        };

        var chapterList = new List<Chapter>()
        {
            CreateChapter("darker than black - Some special", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/darker than black - special.cbz", MangaFormat.Archive), true)
        };

        var actualChapter = chapterList.GetChapterByRange(info);

        Assert.NotEqual(chapterList[0], actualChapter);

    }

    [Fact]
    public void GetAnyChapterByRange_Test_ShouldBeNotNull()
    {
        var info = new ParserInfo()
        {
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume,
            Edition = "",
            Format = MangaFormat.Archive,
            FullFilePath = "/manga/darker than black.cbz",
            Filename = "darker than black.cbz",
            IsSpecial = true,
            Series = "darker than black",
            Title = "darker than black",
            Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume
        };

        var chapterList = new List<Chapter>()
        {
            CreateChapter("darker than black", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), true)
        };

        var actualChapter = chapterList.GetChapterByRange(info);

        Assert.Equal(chapterList[0], actualChapter);
    }

    [Fact]
    public void GetChapterByRange_On_Duplicate_Files_Test_Should_Not_Error()
    {
        var info = new ParserInfo()
        {
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume,
            Edition = "",
            Format = MangaFormat.Archive,
            FullFilePath = "/manga/detective comics #001.cbz",
            Filename = "detective comics #001.cbz",
            IsSpecial = true,
            Series = "detective comics",
            Title = "detective comics",
            Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume
        };

        var chapterList = new List<Chapter>()
        {
            CreateChapter("detective comics", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), true),
            CreateChapter("detective comics", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), true)
        };

        var actualChapter = chapterList.GetChapterByRange(info);

        Assert.Equal(chapterList[0], actualChapter);
    }

    [Fact]
    public void GetChapterByRange_On_FilenameChange_ShouldGetChapter()
    {
        var info = new ParserInfo()
        {
            Chapters = "1",
            Edition = "",
            Format = MangaFormat.Archive,
            FullFilePath = "/manga/detective comics  #001.cbz",
            Filename = "detective comics  #001.cbz",
            IsSpecial = false,
            Series = "detective comics",
            Title = "detective comics",
            Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume
        };

        var chapterList = new List<Chapter>()
        {
            CreateChapter("1", "1", CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), false),
        };

        var actualChapter = chapterList.GetChapterByRange(info);

        Assert.Equal(chapterList[0], actualChapter);
    }

    #region GetFirstChapterWithFiles

    [Fact]
    public void GetFirstChapterWithFiles_ShouldReturnAllChapters()
    {
        var chapterList = new List<Chapter>()
        {
            CreateChapter("darker than black", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), true),
            CreateChapter("darker than black", "1", CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), false),
        };

        Assert.Equal(chapterList.First(), chapterList.GetFirstChapterWithFiles());
    }

    [Fact]
    public void GetFirstChapterWithFiles_ShouldReturnSecondChapter()
    {
        var chapterList = new List<Chapter>()
        {
            CreateChapter("darker than black", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), true),
            CreateChapter("darker than black", "1", CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), false),
        };

        chapterList.First().Files = new List<MangaFile>();

        Assert.Equal(chapterList.Last(), chapterList.GetFirstChapterWithFiles());
    }


    #endregion

    #region MinimumReleaseYear

    [Fact]
    public void MinimumReleaseYear_ZeroIfNoChapters()
    {
        var chapterList = new List<Chapter>();

        Assert.Equal(0, chapterList.MinimumReleaseYear());
    }

    [Fact]
    public void MinimumReleaseYear_ZeroIfNoValidDates()
    {
        var chapterList = new List<Chapter>()
        {
            CreateChapter("detective comics", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), true),
            CreateChapter("detective comics", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), true)
        };

        chapterList[0].ReleaseDate = new DateTime(10, 1, 1);
        chapterList[1].ReleaseDate = DateTime.MinValue;

        Assert.Equal(0, chapterList.MinimumReleaseYear());
    }

    [Fact]
    public void MinimumReleaseYear_MinValidReleaseYear()
    {
        var chapterList = new List<Chapter>()
        {
            CreateChapter("detective comics", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), true),
            CreateChapter("detective comics", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, CreateFile("/manga/detective comics #001.cbz", MangaFormat.Archive), true)
        };

        chapterList[0].ReleaseDate = new DateTime(2002, 1, 1);
        chapterList[1].ReleaseDate = new DateTime(2012, 2, 1);

        Assert.Equal(2002, chapterList.MinimumReleaseYear());
    }

    #endregion
}
