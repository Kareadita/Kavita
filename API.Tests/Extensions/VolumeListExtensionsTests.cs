using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using API.Tests.Helpers;
using Xunit;

namespace API.Tests.Extensions;

public class VolumeListExtensionsTests
{
    #region GetCoverImage

    [Fact]
    public void GetCoverImage_ArchiveFormat()
    {
        var volumes = new List<Volume>()
        {
            new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("3").Build())
                .WithChapter(new ChapterBuilder("4").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),

            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        var v = volumes.GetCoverImage(MangaFormat.Archive);
        Assert.Equal(volumes[0].MinNumber, volumes.GetCoverImage(MangaFormat.Archive).MinNumber);
    }

    [Fact]
    public void GetCoverImage_ChoosesVolume1_WhenHalf()
    {
        var volumes = new List<Volume>()
        {
            new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("0.5").Build())
                .Build(),

            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        var v = volumes.GetCoverImage(MangaFormat.Archive);
        Assert.Equal(volumes[0].MinNumber, volumes.GetCoverImage(MangaFormat.Archive).MinNumber);
    }

    [Fact]
    public void GetCoverImage_EpubFormat()
    {
        var volumes = new List<Volume>()
        {
            new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("3").Build())
                .WithChapter(new ChapterBuilder("4").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Epub).Name);
    }

    [Fact]
    public void GetCoverImage_PdfFormat()
    {
        var volumes = new List<Volume>()
        {
            new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("3").Build())
                .WithChapter(new ChapterBuilder("4").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Pdf).Name);
    }

    [Fact]
    public void GetCoverImage_ImageFormat()
    {
        var volumes = new List<Volume>()
        {
            new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("3").Build())
                .WithChapter(new ChapterBuilder("4").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        Assert.Equal(volumes[0].Name, volumes.GetCoverImage(MangaFormat.Image).Name);
    }

    [Fact]
    public void GetCoverImage_ImageFormat_NoSpecials()
    {
        var volumes = new List<Volume>()
        {
            new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").Build())
                .WithChapter(new ChapterBuilder("4").Build())
                .Build(),
            new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Image).Name);
    }


    #endregion
}
