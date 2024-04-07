using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using API.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Extensions;

public class ParserInfoListExtensions
{
    private readonly IDefaultParser _defaultParser;
    public ParserInfoListExtensions()
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        _defaultParser = new BasicParser(ds, new ImageParser(ds));
    }

    [Theory]
    [InlineData(new[] {"1", "1", "3-5", "5", "8", "0", "0"}, new[] {"1", "3-5", "5", "8", "0"})]
    public void DistinctVolumesTest(string[] volumeNumbers, string[] expectedNumbers)
    {
        var infos = volumeNumbers.Select(n => new ParserInfo() {Series = "", Volumes = n}).ToList();
        Assert.Equal(expectedNumbers, infos.DistinctVolumes());
    }

    [Theory]
    [InlineData(new[] {@"Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip"}, new[] {@"E:\Manga\Cynthia the Mission\Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip"}, true)]
    [InlineData(new[] {@"Cynthia The Mission - c000-006 (v06-07) [Desudesu&Brolen].zip"}, new[] {@"E:\Manga\Cynthia the Mission\Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip"}, false)]
    [InlineData(new[] {@"Cynthia The Mission v20 c12-20 [Desudesu&Brolen].zip"}, new[] {@"E:\Manga\Cynthia the Mission\Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip"}, false)]
    public void HasInfoTest(string[] inputInfos, string[] inputChapters, bool expectedHasInfo)
    {
        var infos = new List<ParserInfo>();
        foreach (var filename in inputInfos)
        {
            infos.Add(_defaultParser.Parse(
                Path.Join("E:/Manga/Cynthia the Mission/", filename),
                "E:/Manga/", "E:/Manga/", LibraryType.Manga));
        }

        var files = inputChapters.Select(s => new MangaFileBuilder(s, MangaFormat.Archive, 199).Build()).ToList();
        var chapter = new ChapterBuilder("0-6")
            .WithFiles(files)
            .Build();

        Assert.Equal(expectedHasInfo, infos.HasInfo(chapter));
    }

    [Fact]
    public void HasInfoTest_SuccessWhenSpecial()
    {
        var infos = new[]
        {
            _defaultParser.Parse(
                "E:/Manga/Cynthia the Mission/Cynthia The Mission The Special SP01 [Desudesu&Brolen].zip",
                "E:/Manga/", "E:/Manga/", LibraryType.Manga)
        };

    var files = new[] {@"E:\Manga\Cynthia the Mission\Cynthia The Mission The Special SP01 [Desudesu&Brolen].zip"}
            .Select(s => new MangaFileBuilder(s, MangaFormat.Archive, 199).Build())
            .ToList();
        var chapter = new ChapterBuilder("Cynthia The Mission The Special SP01 [Desudesu&Brolen].zip")
            .WithRange("Cynthia The Mission The Special SP01 [Desudesu&Brolen]")
            .WithFiles(files)
            .WithIsSpecial(true)
            .Build();

        Assert.True(infos.HasInfo(chapter));
    }
}
