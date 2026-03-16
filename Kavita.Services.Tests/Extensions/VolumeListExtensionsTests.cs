using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests.Extensions;

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
            new VolumeBuilder(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),

            new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
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
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter).Build())
                .Build(),
            new VolumeBuilder(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("0.5").Build())
                .Build(),

            new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
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
            new VolumeBuilder(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
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
            new VolumeBuilder(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
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
            new VolumeBuilder(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build(),
            new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
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
            new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build(),
        };

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Image).Name);
    }


    #endregion
}
