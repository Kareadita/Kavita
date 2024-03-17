using System.IO.Abstractions.TestingHelpers;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Parsing;

public class ImageParsingTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ImageParser _parser;

    public ImageParsingTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        _parser = new ImageParser(directoryService);
    }

    //[Fact]
    public void Parse_ParseInfo_Manga_ImageOnly()
    {
        // Images don't have root path as E:\Manga, but rather as the path of the folder

        // Note: Fallback to folder will parse Monster #8 and get Monster
        var filepath = @"E:\Manga\Monster #8\Ch. 001-016 [MangaPlus] [Digital] [amit34521]\Monster #8 Ch. 001 [MangaPlus] [Digital] [amit34521]\13.jpg";
        var expectedInfo2 = new ParserInfo
        {
            Series = "Monster #8", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
            Chapters = "8", Filename = "13.jpg", Format = MangaFormat.Image,
            FullFilePath = filepath, IsSpecial = false
        };
        var actual2 = _parser.Parse(filepath, @"E:\Manga\Monster #8", "E:/Manga", LibraryType.Image, null);
        Assert.NotNull(actual2);
        _testOutputHelper.WriteLine($"Validating {filepath}");
        Assert.Equal(expectedInfo2.Format, actual2.Format);
        _testOutputHelper.WriteLine("Format ✓");
        Assert.Equal(expectedInfo2.Series, actual2.Series);
        _testOutputHelper.WriteLine("Series ✓");
        Assert.Equal(expectedInfo2.Chapters, actual2.Chapters);
        _testOutputHelper.WriteLine("Chapters ✓");
        Assert.Equal(expectedInfo2.Volumes, actual2.Volumes);
        _testOutputHelper.WriteLine("Volumes ✓");
        Assert.Equal(expectedInfo2.Edition, actual2.Edition);
        _testOutputHelper.WriteLine("Edition ✓");
        Assert.Equal(expectedInfo2.Filename, actual2.Filename);
        _testOutputHelper.WriteLine("Filename ✓");
        Assert.Equal(expectedInfo2.FullFilePath, actual2.FullFilePath);
        _testOutputHelper.WriteLine("FullFilePath ✓");

        filepath = @"E:\Manga\Extra layer for no reason\Just Images the second\Vol19\ch. 186\Vol. 19 p106.gif";
        expectedInfo2 = new ParserInfo
        {
            Series = "Just Images the second", Volumes = "19", Edition = "",
            Chapters = "186", Filename = "Vol. 19 p106.gif", Format = MangaFormat.Image,
            FullFilePath = filepath, IsSpecial = false
        };

        actual2 = _parser.Parse(filepath, @"E:\Manga\Extra layer for no reason\", "E:/Manga", LibraryType.Image, null);
        Assert.NotNull(actual2);
        _testOutputHelper.WriteLine($"Validating {filepath}");
        Assert.Equal(expectedInfo2.Format, actual2.Format);
        _testOutputHelper.WriteLine("Format ✓");
        Assert.Equal(expectedInfo2.Series, actual2.Series);
        _testOutputHelper.WriteLine("Series ✓");
        Assert.Equal(expectedInfo2.Chapters, actual2.Chapters);
        _testOutputHelper.WriteLine("Chapters ✓");
        Assert.Equal(expectedInfo2.Volumes, actual2.Volumes);
        _testOutputHelper.WriteLine("Volumes ✓");
        Assert.Equal(expectedInfo2.Edition, actual2.Edition);
        _testOutputHelper.WriteLine("Edition ✓");
        Assert.Equal(expectedInfo2.Filename, actual2.Filename);
        _testOutputHelper.WriteLine("Filename ✓");
        Assert.Equal(expectedInfo2.FullFilePath, actual2.FullFilePath);
        _testOutputHelper.WriteLine("FullFilePath ✓");

        filepath = @"E:\Manga\Extra layer for no reason\Just Images the second\Blank Folder\Vol19\ch. 186\Vol. 19 p106.gif";
        expectedInfo2 = new ParserInfo
        {
            Series = "Just Images the second", Volumes = "19", Edition = "",
            Chapters = "186", Filename = "Vol. 19 p106.gif", Format = MangaFormat.Image,
            FullFilePath = filepath, IsSpecial = false
        };

        actual2 = _parser.Parse(filepath, @"E:\Manga\Extra layer for no reason\", "E:/Manga", LibraryType.Image, null);
        Assert.NotNull(actual2);
        _testOutputHelper.WriteLine($"Validating {filepath}");
        Assert.Equal(expectedInfo2.Format, actual2.Format);
        _testOutputHelper.WriteLine("Format ✓");
        Assert.Equal(expectedInfo2.Series, actual2.Series);
        _testOutputHelper.WriteLine("Series ✓");
        Assert.Equal(expectedInfo2.Chapters, actual2.Chapters);
        _testOutputHelper.WriteLine("Chapters ✓");
        Assert.Equal(expectedInfo2.Volumes, actual2.Volumes);
        _testOutputHelper.WriteLine("Volumes ✓");
        Assert.Equal(expectedInfo2.Edition, actual2.Edition);
        _testOutputHelper.WriteLine("Edition ✓");
        Assert.Equal(expectedInfo2.Filename, actual2.Filename);
        _testOutputHelper.WriteLine("Filename ✓");
        Assert.Equal(expectedInfo2.FullFilePath, actual2.FullFilePath);
        _testOutputHelper.WriteLine("FullFilePath ✓");
    }
}
