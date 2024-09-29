using System.Linq;
using API.Comparators;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesExtensionsTests
{
    [Fact]
    public void GetCoverImage_MultipleSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Special 1")
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
                    .Build())
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Special 2")
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 2)
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Special 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_Volume1Chapter1_Volume2_AndLooseChapters()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("13")
                    .WithCoverImage("Chapter 13")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithName("Volume 1")
                .WithChapter(new ChapterBuilder("1")
                    .WithCoverImage("Volume 1 Chapter 1")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithName("Volume 2")
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Volume 2")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1 Chapter 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_LooseChapters_WithSub1_Chapter()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("-1")
                    .WithCoverImage("Chapter -1")
                    .Build())
                .WithChapter(new ChapterBuilder("0.5")
                    .WithCoverImage("Chapter 0.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithCoverImage("Chapter 2")
                    .Build())
                .WithChapter(new ChapterBuilder("1")
                    .WithCoverImage("Chapter 1")
                    .Build())
                .WithChapter(new ChapterBuilder("3")
                    .WithCoverImage("Chapter 3")
                    .Build())
                .WithChapter(new ChapterBuilder("4AU")
                    .WithCoverImage("Chapter 4AU")
                    .Build())
                .Build())

            .Build();


        Assert.Equal("Chapter 1", series.GetCoverImage());
    }

    /// <summary>
    /// Checks the case where there are specials and loose leafs, loose leaf chapters should be preferred
    /// </summary>
    [Fact]
    public void GetCoverImage_LooseChapters_WithSub1_Chapter_WithSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)

            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithName(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("I am a Special")
                    .WithCoverImage("I am a Special")
                    .Build())
                .WithChapter(new ChapterBuilder("I am a Special 2")
                    .WithCoverImage("I am a Special 2")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("0.5")
                    .WithCoverImage("Chapter 0.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithCoverImage("Chapter 2")
                    .Build())
                .WithChapter(new ChapterBuilder("1")
                    .WithCoverImage("Chapter 1")
                    .Build())
                .Build())

            .Build();


        Assert.Equal("Chapter 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustVolumes()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)

            .WithVolume(new VolumeBuilder("1")
                .WithName("Volume 1")
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Volume 1 Chapter 1")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithName("Volume 2")
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Volume 2")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithName("Volume 3")
                .WithChapter(new ChapterBuilder("10")
                    .WithCoverImage("Volume 3 Chapter 10")
                    .Build())
                .WithChapter(new ChapterBuilder("11")
                    .WithCoverImage("Volume 3 Chapter 11")
                    .Build())
                .WithChapter(new ChapterBuilder("12")
                    .WithCoverImage("Volume 3 Chapter 12")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1 Chapter 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustVolumes_ButVolume0()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)

            .WithVolume(new VolumeBuilder("0")
                .WithName("Volume 0")
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Volume 0")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithName("Volume 1")
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithCoverImage("Volume 1")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.SortOrder, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustSpecials_WithDecimal()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Special 1")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Special 2")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Special 2", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustChaptersAndSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 1")
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
                    .Build())
            .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Chapter 2", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChapters()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 3")
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 1")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChaptersAndSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 1")
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 1")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChaptersAndSpecials_Ippo()
    {
        var series = new SeriesBuilder("Ippo")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1426")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 1426")
                    .Build())
                .WithChapter(new ChapterBuilder("1425")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 1425")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 3")
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 1")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("137")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 137")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChapters_WhereVolumeIsNot1()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("4")
                .WithMinNumber(4)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 4")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => x.MinNumber, ChapterSortComparerDefaultFirst.Default)?.CoverImage;
        }

        Assert.Equal("Chapter 2", series.GetCoverImage());
    }

    /// <summary>
    /// Ensure that Series cover is issue 1, when there are less than 1 entities and specials
    /// </summary>
    [Fact]
    public void GetCoverImage_LessThanIssue1()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 0")
                    .Build())
                .WithChapter(new ChapterBuilder("1")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 1")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithMinNumber(4)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 4")
                    .Build())
                .Build())
            .Build();

        Assert.Equal("Chapter 1", series.GetCoverImage());
    }

    /// <summary>
    /// Ensure that Series cover is issue 1, when there are less than 1 entities and specials
    /// </summary>
    [Fact]
    public void GetCoverImage_LessThanIssue1_WithNegative()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithName(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("-1")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter -1")
                    .Build())
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 0")
                    .Build())
                .WithChapter(new ChapterBuilder("1")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 1")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithMinNumber(4)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 4")
                    .Build())
                .Build())
            .Build();

        Assert.Equal("Chapter 1", series.GetCoverImage());
    }


}
