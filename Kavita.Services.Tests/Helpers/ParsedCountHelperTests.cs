using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class ParsedCountHelperTests
{
    #region GetCalculatedCount

    [Fact]
    public void GetCalculatedCount_ChapterOnly()
    {
        var info = new ParserInfo { Series = "Test", HighestChapter = 5, HighestVolume = 0 };
        Assert.Equal(5, ParsedCountHelper.GetCalculatedCount(info));
    }

    [Fact]
    public void GetCalculatedCount_VolumeOnly()
    {
        var info = new ParserInfo { Series = "Test", HighestChapter = 0, HighestVolume = 10 };
        Assert.Equal(10, ParsedCountHelper.GetCalculatedCount(info));
    }

    [Fact]
    public void GetCalculatedCount_Both_ChapterTakesPriority()
    {
        var info = new ParserInfo { Series = "Test", HighestChapter = 5, HighestVolume = 10 };
        Assert.Equal(5, ParsedCountHelper.GetCalculatedCount(info));
    }

    [Fact]
    public void GetCalculatedCount_DecimalChapter_Floors()
    {
        var info = new ParserInfo { Series = "Test", HighestChapter = 5.7f, HighestVolume = 0 };
        Assert.Equal(5, ParsedCountHelper.GetCalculatedCount(info));
    }

    [Fact]
    public void GetCalculatedCount_DecimalVolume_Floors()
    {
        var info = new ParserInfo { Series = "Test", HighestChapter = 0, HighestVolume = 3.9f };
        Assert.Equal(3, ParsedCountHelper.GetCalculatedCount(info));
    }

    [Fact]
    public void GetCalculatedCount_NeitherSet_ReturnsZero()
    {
        var info = new ParserInfo { Series = "Test", HighestChapter = 0, HighestVolume = 0 };
        Assert.Equal(0, ParsedCountHelper.GetCalculatedCount(info));
    }

    #endregion

    #region GetTotalCount

    [Fact]
    public void GetTotalCount_ComicInfoCountSet()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            ComicInfo = new ComicInfo { Count = 10 },
            HasEndMarker = false
        };
        Assert.Equal(10, ParsedCountHelper.GetTotalCount(info));
    }

    [Fact]
    public void GetTotalCount_ComicInfoCount_TakesPriorityOverEndMarker()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            ComicInfo = new ComicInfo { Count = 10 },
            HasEndMarker = true,
            HighestChapter = 5,
            HighestVolume = 3
        };
        Assert.Equal(10, ParsedCountHelper.GetTotalCount(info));
    }

    [Fact]
    public void GetTotalCount_EndMarker_ChapterHigherThanVolume()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            HasEndMarker = true,
            HighestChapter = 20,
            HighestVolume = 5
        };
        Assert.Equal(20, ParsedCountHelper.GetTotalCount(info));
    }

    [Fact]
    public void GetTotalCount_EndMarker_VolumeHigherThanChapter()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            HasEndMarker = true,
            HighestChapter = 3,
            HighestVolume = 15
        };
        Assert.Equal(15, ParsedCountHelper.GetTotalCount(info));
    }

    [Fact]
    public void GetTotalCount_NoComicInfoCount_NoEndMarker_ReturnsNull()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            ComicInfo = null,
            HasEndMarker = false
        };
        Assert.Null(ParsedCountHelper.GetTotalCount(info));
    }

    [Fact]
    public void GetTotalCount_ComicInfoCountZero_NoEndMarker_ReturnsNull()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            ComicInfo = new ComicInfo { Count = 0 },
            HasEndMarker = false
        };
        Assert.Null(ParsedCountHelper.GetTotalCount(info));
    }

    [Fact]
    public void GetTotalCount_ComicInfoCountZero_WithEndMarker_UsesEndMarker()
    {
        var info = new ParserInfo
        {
            Series = "Test",
            ComicInfo = new ComicInfo { Count = 0 },
            HasEndMarker = true,
            HighestChapter = 8,
            HighestVolume = 2
        };
        Assert.Equal(8, ParsedCountHelper.GetTotalCount(info));
    }

    #endregion
}
