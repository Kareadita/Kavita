using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Parsers;

public class DefaultParserTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly DefaultParser _defaultParser;

    public DefaultParserTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        _defaultParser = new BasicParser(directoryService, new ImageParser(directoryService));
    }




    #region ParseFromFallbackFolders
    [Theory]
    [InlineData("C:/", "C:/Love Hina/Love Hina - Special.cbz", "Love Hina")]
    [InlineData("C:/", "C:/Love Hina/Specials/Ani-Hina Art Collection.cbz", "Love Hina")]
    [InlineData("C:/", "C:/Mujaki no Rakuen Something/Mujaki no Rakuen Vol12 ch76.cbz", "Mujaki no Rakuen")]
    [InlineData("C:/", "C:/Something Random/Mujaki no Rakuen SP01.cbz", "Something Random")]
    public void ParseFromFallbackFolders_FallbackShouldParseSeries(string rootDir, string inputPath, string expectedSeries)
    {
        var actual = _defaultParser.Parse(inputPath, rootDir, rootDir, LibraryType.Manga, null);
        if (actual == null)
        {
            Assert.NotNull(actual);
            return;
        }

        Assert.Equal(expectedSeries, actual.Series);
    }

    [Theory]
    [InlineData("/manga/Btooom!/Vol.1/Chapter 1/1.cbz", new [] {"Btooom!", "1", "1"})]
    [InlineData("/manga/Btooom!/Vol.1 Chapter 2/1.cbz", new [] {"Btooom!", "1", "2"})]
    [InlineData("/manga/Monster/Ch. 001-016 [MangaPlus] [Digital] [amit34521]/Monster Ch. 001 [MangaPlus] [Digital] [amit34521]/13.jpg", new [] {"Monster", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, "1"})]
    [InlineData("/manga/Hajime no Ippo/Artbook/Hajime no Ippo - Artbook.cbz", new [] {"Hajime no Ippo", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter})]
    public void ParseFromFallbackFolders_ShouldParseSeriesVolumeAndChapter(string inputFile, string[] expectedParseInfo)
    {
        const string rootDirectory = "/manga/";
        var actual = new ParserInfo {Series = "", Chapters = Parser.DefaultChapter, Volumes = Parser.LooseLeafVolume};
        _defaultParser.ParseFromFallbackFolders(inputFile, rootDirectory, LibraryType.Manga, ref actual);
        Assert.Equal(expectedParseInfo[0], actual.Series);
        Assert.Equal(expectedParseInfo[1], actual.Volumes);
        Assert.Equal(expectedParseInfo[2], actual.Chapters);
    }

    [Theory]
    [InlineData("/manga/Btooom!/Vol.1/Chapter 1/1.cbz", "Btooom!")]
    [InlineData("/manga/Btooom!/Vol.1 Chapter 2/1.cbz", "Btooom!")]
    [InlineData("/manga/Monster #8 (Digital)/Ch. 001-016 [MangaPlus] [Digital] [amit34521]/Monster #8 Ch. 001 [MangaPlus] [Digital] [amit34521]/13.jpg", "manga")]
    [InlineData("/manga/Monster (Digital)/Ch. 001-016 [MangaPlus] [Digital] [amit34521]/Monster Ch. 001 [MangaPlus] [Digital] [amit34521]/13.jpg", "Monster")]
    [InlineData("/manga/Foo 50/Specials/Foo 50 SP01.cbz", "Foo 50")]
    [InlineData("/manga/Foo 50 (kiraa)/Specials/Foo 50 SP01.cbz", "Foo 50")]
    [InlineData("/manga/Btooom!/Specials/Just a special SP01.cbz", "Btooom!")]
    public void ParseFromFallbackFolders_ShouldUseExistingSeriesName(string inputFile, string expectedParseInfo)
    {
        const string rootDirectory = "/manga/";
        var fs = new MockFileSystem();
        fs.AddDirectory(rootDirectory);
        fs.AddFile(inputFile, new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var parser = new BasicParser(ds, new ImageParser(ds));
        var actual = parser.Parse(inputFile, rootDirectory, rootDirectory, LibraryType.Manga, null);
        _defaultParser.ParseFromFallbackFolders(inputFile, rootDirectory, LibraryType.Manga, ref actual);
        Assert.Equal(expectedParseInfo, actual.Series);
    }

    [Theory]
    [InlineData("/manga/Btooom!/Specials/Art Book.cbz", "Btooom!")]
    [InlineData("/manga/Hajime no Ippo/Artbook/Hajime no Ippo - Artbook.cbz", "Hajime no Ippo")]
    public void ParseFromFallbackFolders_ShouldUseExistingSeriesName_NewScanLoop(string inputFile, string expectedParseInfo)
    {
        const string rootDirectory = "/manga/";
        var fs = new MockFileSystem();
        fs.AddDirectory(rootDirectory);
        fs.AddFile(inputFile, new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var parser = new BasicParser(ds, new ImageParser(ds));
        var actual = parser.Parse(inputFile, rootDirectory, rootDirectory, LibraryType.Manga, null);
        _defaultParser.ParseFromFallbackFolders(inputFile, rootDirectory, LibraryType.Manga, ref actual);
        Assert.Equal(expectedParseInfo, actual.Series);
    }

    #endregion


    #region Parse


    [Fact]
    public void Parse_ParseInfo_Manga()
    {
        const string rootPath = @"E:/Manga/";
        var expected = new Dictionary<string, ParserInfo>();

        var filepath = @"E:/Manga/Mujaki no Rakuen/Mujaki no Rakuen Vol12 ch76.cbz";
         expected.Add(filepath, new ParserInfo
         {
             Series = "Mujaki no Rakuen", Volumes = "12",
             Chapters = "76", Filename = "Mujaki no Rakuen Vol12 ch76.cbz", Format = MangaFormat.Archive,
             FullFilePath = filepath
         });

         filepath = @"E:/Manga/Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen/Vol 1.cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen", Volumes = "1",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Vol 1.cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Beelzebub/Beelzebub_01_[Noodles].zip";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Beelzebub", Volumes = Parser.LooseLeafVolume,
            Chapters = "1", Filename = "Beelzebub_01_[Noodles].zip", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        // Note: Lots of duplicates here. I think I can move them to the ParserTests itself
        filepath = @"E:/Manga/Ichinensei ni Nacchattara/Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Ichinensei ni Nacchattara", Volumes = "1",
            Chapters = "1", Filename = "Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Tenjo Tenge (Color)/Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Tenjo Tenge {Full Contact Edition}", Volumes = "1", Edition = "",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)/Akame ga KILL! ZERO v01 (2016) (Digital) (LuCaZ).cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Akame ga KILL! ZERO", Volumes = "1", Edition = "",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Akame ga KILL! ZERO v01 (2016) (Digital) (LuCaZ).cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Dorohedoro/Dorohedoro v01 (2010) (Digital) (LostNerevarine-Empire).cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Dorohedoro", Volumes = "1", Edition = "",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Dorohedoro v01 (2010) (Digital) (LostNerevarine-Empire).cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/APOSIMZ/APOSIMZ 040 (2020) (Digital) (danke-Empire).cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "APOSIMZ", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
            Chapters = "40", Filename = "APOSIMZ 040 (2020) (Digital) (danke-Empire).cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Corpse Party Musume/Kedouin Makoto - Corpse Party Musume, Chapter 09.cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Kedouin Makoto - Corpse Party Musume", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
            Chapters = "9", Filename = "Kedouin Makoto - Corpse Party Musume, Chapter 09.cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Goblin Slayer/Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire).cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Goblin Slayer - Brand New Day", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
            Chapters = "6.5", Filename = "Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire).cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        });

        filepath = @"E:/Manga/Summer Time Rendering/Specials/Record 014 (between chapter 083 and ch084) SP11.cbr";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Summer Time Rendering", Volumes = API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume, Edition = "",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Record 014 (between chapter 083 and ch084) SP11.cbr", Format = MangaFormat.Archive,
            FullFilePath = filepath, IsSpecial = true
        });

        filepath = @"E:/Manga/Seraph of the End/Seraph of the End - Vampire Reign 093 (2020) (Digital) (LuCaZ).cbz";
        expected.Add(filepath, new ParserInfo
        {
          Series = "Seraph of the End - Vampire Reign", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
          Chapters = "93", Filename = "Seraph of the End - Vampire Reign 093 (2020) (Digital) (LuCaZ).cbz", Format = MangaFormat.Archive,
          FullFilePath = filepath, IsSpecial = false
        });

        filepath = @"E:/Manga/Kono Subarashii Sekai ni Bakuen wo!/Vol. 00 Ch. 000.cbz";
        expected.Add(filepath, new ParserInfo
        {
          Series = "Kono Subarashii Sekai ni Bakuen wo!", Volumes = "0", Edition = "",
          Chapters = "0", Filename = "Vol. 00 Ch. 000.cbz", Format = MangaFormat.Archive,
          FullFilePath = filepath, IsSpecial = false
        });

        filepath = @"E:/Manga/Toukyou Akazukin/Vol. 01 Ch. 001.cbz";
        expected.Add(filepath, new ParserInfo
        {
          Series = "Toukyou Akazukin", Volumes = "1", Edition = "",
          Chapters = "1", Filename = "Vol. 01 Ch. 001.cbz", Format = MangaFormat.Archive,
          FullFilePath = filepath, IsSpecial = false
        });

        // If an image is cover exclusively, ignore it
        filepath = @"E:/Manga/Seraph of the End/cover.png";
        expected.Add(filepath, null);

        filepath = @"E:/Manga/The Beginning After the End/Chapter 001.cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "The Beginning After the End", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
            Chapters = "1", Filename = "Chapter 001.cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath, IsSpecial = false
        });

        filepath = @"E:/Manga/Air Gear/Air Gear Omnibus v01 (2016) (Digital) (Shadowcat-Empire).cbz";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Air Gear", Volumes = "1", Edition = "Omnibus",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Air Gear Omnibus v01 (2016) (Digital) (Shadowcat-Empire).cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath, IsSpecial = false
        });

        filepath = @"E:/Manga/Harrison, Kim - The Good, The Bad, and the Undead - Hollows Vol 2.5.epub";
        expected.Add(filepath, new ParserInfo
        {
            Series = "Harrison, Kim - The Good, The Bad, and the Undead - Hollows", Volumes = "2.5", Edition = "",
            Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Harrison, Kim - The Good, The Bad, and the Undead - Hollows Vol 2.5.epub", Format = MangaFormat.Epub,
            FullFilePath = filepath, IsSpecial = false
        });

        foreach (var file in expected.Keys)
        {
            var expectedInfo = expected[file];
            var actual = _defaultParser.Parse(file, rootPath, rootPath, LibraryType.Manga, null);
            if (expectedInfo == null)
            {
                Assert.Null(actual);
                continue;
            }
            Assert.NotNull(actual);
            _testOutputHelper.WriteLine($"Validating {file}");
            Assert.Equal(expectedInfo.Format, actual.Format);
            _testOutputHelper.WriteLine("Format ✓");
            Assert.Equal(expectedInfo.Series, actual.Series);
            _testOutputHelper.WriteLine("Series ✓");
            Assert.Equal(expectedInfo.Chapters, actual.Chapters);
            _testOutputHelper.WriteLine("Chapters ✓");
            Assert.Equal(expectedInfo.Volumes, actual.Volumes);
            _testOutputHelper.WriteLine("Volumes ✓");
            Assert.Equal(expectedInfo.Edition, actual.Edition);
            _testOutputHelper.WriteLine("Edition ✓");
            Assert.Equal(expectedInfo.Filename, actual.Filename);
            _testOutputHelper.WriteLine("Filename ✓");
            Assert.Equal(expectedInfo.FullFilePath, actual.FullFilePath);
            _testOutputHelper.WriteLine("FullFilePath ✓");
        }
    }

    //[Fact]
    public void Parse_ParseInfo_Manga_ImageOnly()
    {
        // Images don't have root path as E:/Manga, but rather as the path of the folder

        // Note: Fallback to folder will parse Monster #8 and get Monster
        var filepath = @"E:/Manga/Monster #8/Ch. 001-016 [MangaPlus] [Digital] [amit34521]/Monster #8 Ch. 001 [MangaPlus] [Digital] [amit34521]/13.jpg";
        var expectedInfo2 = new ParserInfo
        {
            Series = "Monster #8", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
            Chapters = "8", Filename = "13.jpg", Format = MangaFormat.Image,
            FullFilePath = filepath, IsSpecial = false
        };
        var actual2 = _defaultParser.Parse(filepath, @"E:/Manga/Monster #8", "E:/Manga", LibraryType.Manga, null);
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

        filepath = @"E:/Manga/Extra layer for no reason/Just Images the second/Vol19/ch. 186/Vol. 19 p106.gif";
        expectedInfo2 = new ParserInfo
        {
            Series = "Just Images the second", Volumes = "19", Edition = "",
            Chapters = "186", Filename = "Vol. 19 p106.gif", Format = MangaFormat.Image,
            FullFilePath = filepath, IsSpecial = false
        };

        actual2 = _defaultParser.Parse(filepath, @"E:/Manga/Extra layer for no reason/", "E:/Manga",LibraryType.Manga, null);
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

        filepath = @"E:/Manga/Extra layer for no reason/Just Images the second/Blank Folder/Vol19/ch. 186/Vol. 19 p106.gif";
        expectedInfo2 = new ParserInfo
        {
            Series = "Just Images the second", Volumes = "19", Edition = "",
            Chapters = "186", Filename = "Vol. 19 p106.gif", Format = MangaFormat.Image,
            FullFilePath = filepath, IsSpecial = false
        };

        actual2 = _defaultParser.Parse(filepath, @"E:/Manga/Extra layer for no reason/", "E:/Manga", LibraryType.Manga, null);
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

    [Fact]
    public void Parse_ParseInfo_Manga_WithSpecialsFolder()
    {
        const string rootPath = @"E:/Manga/";
        var filesystem = new MockFileSystem();
        filesystem.AddDirectory("E:/Manga");
        filesystem.AddDirectory("E:/Foo 50");
        filesystem.AddDirectory("E:/Foo 50/Specials");
        filesystem.AddFile(@"E:/Manga/Foo 50/Foo 50 v1.cbz", new MockFileData(""));
        filesystem.AddFile(@"E:/Manga/Foo 50/Specials/Foo 50 SP01.cbz", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var parser = new BasicParser(ds, new ImageParser(ds));

        var filepath = @"E:/Manga/Foo 50/Foo 50 v1.cbz";
        // There is a bad parse for series like "Foo 50", so we have parsed chapter as 50
        var expected = new ParserInfo
        {
            Series = "Foo 50", Volumes = "1",
            Chapters = "50", Filename = "Foo 50 v1.cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        };

        var actual = parser.Parse(filepath, rootPath, rootPath, LibraryType.Manga, null);

        Assert.NotNull(actual);
        _testOutputHelper.WriteLine($"Validating {filepath}");
        Assert.Equal(expected.Format, actual.Format);
        _testOutputHelper.WriteLine("Format ✓");
        Assert.Equal(expected.Series, actual.Series);
        _testOutputHelper.WriteLine("Series ✓");
        Assert.Equal(expected.Chapters, actual.Chapters);
        _testOutputHelper.WriteLine("Chapters ✓");
        Assert.Equal(expected.Volumes, actual.Volumes);
        _testOutputHelper.WriteLine("Volumes ✓");
        Assert.Equal(expected.Edition, actual.Edition);
        _testOutputHelper.WriteLine("Edition ✓");
        Assert.Equal(expected.Filename, actual.Filename);
        _testOutputHelper.WriteLine("Filename ✓");
        Assert.Equal(expected.FullFilePath, actual.FullFilePath);
        _testOutputHelper.WriteLine("FullFilePath ✓");
        Assert.Equal(expected.IsSpecial, actual.IsSpecial);
        _testOutputHelper.WriteLine("IsSpecial ✓");

        filepath = @"E:/Manga/Foo 50/Specials/Foo 50 SP01.cbz";
        expected = new ParserInfo
        {
            Series = "Foo 50", Volumes = API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume, IsSpecial = true,
            Chapters = Parser.DefaultChapter, Filename = "Foo 50 SP01.cbz", Format = MangaFormat.Archive,
            FullFilePath = filepath
        };

        actual = parser.Parse(filepath, rootPath, rootPath, LibraryType.Manga, null);
        Assert.NotNull(actual);
        _testOutputHelper.WriteLine($"Validating {filepath}");
        Assert.Equal(expected.Format, actual.Format);
        _testOutputHelper.WriteLine("Format ✓");
        Assert.Equal(expected.Series, actual.Series);
        _testOutputHelper.WriteLine("Series ✓");
        Assert.Equal(expected.Chapters, actual.Chapters);
        _testOutputHelper.WriteLine("Chapters ✓");
        Assert.Equal(expected.Volumes, actual.Volumes);
        _testOutputHelper.WriteLine("Volumes ✓");
        Assert.Equal(expected.Edition, actual.Edition);
        _testOutputHelper.WriteLine("Edition ✓");
        Assert.Equal(expected.Filename, actual.Filename);
        _testOutputHelper.WriteLine("Filename ✓");
        Assert.Equal(expected.FullFilePath, actual.FullFilePath);
        _testOutputHelper.WriteLine("FullFilePath ✓");
        Assert.Equal(expected.IsSpecial, actual.IsSpecial);
        _testOutputHelper.WriteLine("IsSpecial ✓");

    }

    [Fact]
    public void Parse_ParseInfo_Comic()
        {
            const string rootPath = "E:/Comics/";
            var expected = new Dictionary<string, ParserInfo>();
            var filepath = @"E:/Comics/Teen Titans/Teen Titans v1 Annual 01 (1967) SP01.cbr";
             expected.Add(filepath, new ParserInfo
             {
                 Series = "Teen Titans", Volumes = API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume,
                 Chapters = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, Filename = "Teen Titans v1 Annual 01 (1967) SP01.cbr", Format = MangaFormat.Archive,
                 FullFilePath = filepath
             });

             // Fallback test with bad naming
             filepath = @"E:/Comics/Comics/Babe/Babe Vol.1 #1-4/Babe 01.cbr";
             expected.Add(filepath, new ParserInfo
             {
                 Series = "Babe", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
                 Chapters = "1", Filename = "Babe 01.cbr", Format = MangaFormat.Archive,
                 FullFilePath = filepath, IsSpecial = false
             });

             filepath = @"E:/Comics/Comics/Publisher/Batman the Detective (2021)/Batman the Detective - v6 - 11 - (2021).cbr";
             expected.Add(filepath, new ParserInfo
             {
                 Series = "Batman the Detective", Volumes = "6", Edition = "",
                 Chapters = "11", Filename = "Batman the Detective - v6 - 11 - (2021).cbr", Format = MangaFormat.Archive,
                 FullFilePath = filepath, IsSpecial = false
             });

             filepath = @"E:/Comics/Comics/Batman - The Man Who Laughs #1 (2005)/Batman - The Man Who Laughs #1 (2005).cbr";
             expected.Add(filepath, new ParserInfo
             {
                 Series = "Batman - The Man Who Laughs", Volumes = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume, Edition = "",
                 Chapters = "1", Filename = "Batman - The Man Who Laughs #1 (2005).cbr", Format = MangaFormat.Archive,
                 FullFilePath = filepath, IsSpecial = false
             });

            foreach (var file in expected.Keys)
            {
                var expectedInfo = expected[file];
                var actual = _defaultParser.Parse(file, rootPath, rootPath, LibraryType.Comic, null);
                if (expectedInfo == null)
                {
                    Assert.Null(actual);
                    continue;
                }
                Assert.NotNull(actual);
                _testOutputHelper.WriteLine($"Validating {file}");
                Assert.Equal(expectedInfo.Format, actual.Format);
                _testOutputHelper.WriteLine("Format ✓");
                Assert.Equal(expectedInfo.Series, actual.Series);
                _testOutputHelper.WriteLine("Series ✓");
                Assert.Equal(expectedInfo.Chapters, actual.Chapters);
                _testOutputHelper.WriteLine("Chapters ✓");
                Assert.Equal(expectedInfo.Volumes, actual.Volumes);
                _testOutputHelper.WriteLine("Volumes ✓");
                Assert.Equal(expectedInfo.Edition, actual.Edition);
                _testOutputHelper.WriteLine("Edition ✓");
                Assert.Equal(expectedInfo.Filename, actual.Filename);
                _testOutputHelper.WriteLine("Filename ✓");
                Assert.Equal(expectedInfo.FullFilePath, actual.FullFilePath);
                _testOutputHelper.WriteLine("FullFilePath ✓");
            }
        }
    #endregion
}
