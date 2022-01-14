using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Tests.Helpers;
using Xunit;

namespace API.Tests.Extensions;

public class VolumeListExtensionsTests
{
    #region FirstWithChapters

    [Fact]
    public void FirstWithChapters_ReturnsVolumeWithChapters()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("0", new List<Chapter>()),
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
            }),
        };

        Assert.Equal(volumes[1].Number, volumes.FirstWithChapters(false).Number);
        Assert.Equal(volumes[1].Number, volumes.FirstWithChapters(true).Number);
    }

    [Fact]
    public void FirstWithChapters_Book()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("0", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[1].Number, volumes.FirstWithChapters(true).Number);
    }

    [Fact]
    public void FirstWithChapters_NonBook()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("0", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[1].Number, volumes.FirstWithChapters(false).Number);
    }

    #endregion

    #region GetCoverImage

    [Fact]
    public void GetCoverImage_ArchiveFormat()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("0", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[0].Number, volumes.GetCoverImage(MangaFormat.Archive).Number);
    }

    [Fact]
    public void GetCoverImage_EpubFormat()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("0", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[1].Number, volumes.GetCoverImage(MangaFormat.Epub).Number);
    }

    [Fact]
    public void GetCoverImage_PdfFormat()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("0", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[1].Number, volumes.GetCoverImage(MangaFormat.Pdf).Number);
    }

    [Fact]
    public void GetCoverImage_ImageFormat()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("0", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[1].Number, volumes.GetCoverImage(MangaFormat.Image).Number);
    }


    #endregion
}
