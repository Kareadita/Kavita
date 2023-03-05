using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Tests.Helpers.Builders;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesExtensionsTests
{
    [Fact]
    public void GetCoverImage_MultipleSpecials_Comics()
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

        Assert.Equal("Special 1", series.GetCoverImage());

    }

    [Fact]
    public void GetCoverImage_MultipleSpecials_Books()
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

        Assert.Equal("Special 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustChapters_Comics()
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
    public void GetCoverImage_JustChaptersAndSpecials_Comics()
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
                .WithChapter(new ChapterBuilder("0")
                    .WithIsSpecial(true)
                    .WithCoverImage("Special 3")
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
    public void GetCoverImage_VolumesChapters_Comics()
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
    public void GetCoverImage_VolumesChaptersAndSpecials_Comics()
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


}
