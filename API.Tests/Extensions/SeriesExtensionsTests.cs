using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesExtensionsTests
{
    [Fact]
    public void GetCoverImage_MultipleSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0")
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithCoverImage("Special 1")
                    .WithIsSpecial(true)
                    .Build())
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithCoverImage("Special 2")
                    .WithIsSpecial(true)
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Special 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_Volume1Chapter1_Volume2_AndLooseChapters()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
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
                .WithChapter(new ChapterBuilder("0")
                    .WithCoverImage("Volume 2")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1 Chapter 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustVolumes()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)

            .WithVolume(new VolumeBuilder("1")
                .WithName("Volume 1")
                .WithChapter(new ChapterBuilder("0")
                    .WithCoverImage("Volume 1 Chapter 1")
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithName("Volume 2")
                .WithChapter(new ChapterBuilder("0")
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
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1 Chapter 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustSpecials_WithDecimal()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0")
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
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
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Special 2", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustChaptersAndSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0")
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 1")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Chapter 2", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChapters()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0")
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 3")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithNumber(1)
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 1")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChaptersAndSpecials()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0")
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
                .WithChapter(new ChapterBuilder("2.5")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2.5")
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithIsSpecial(false)
                    .WithCoverImage("Chapter 2")
                    .Build())
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 1")
                    .Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithNumber(1)
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 1")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChapters_WhereVolumeIsNot1()
    {
        var series = new SeriesBuilder("Test 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0")
                .WithName(API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume)
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
                .WithNumber(4)
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(false)
                    .WithCoverImage("Volume 4")
                    .Build())
                .Build())
            .Build();

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Chapter 2", series.GetCoverImage());
    }


}
