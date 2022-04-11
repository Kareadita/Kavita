using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
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

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Epub).Name);
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

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Pdf).Name);
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

        Assert.Equal(volumes[0].Name, volumes.GetCoverImage(MangaFormat.Image).Name);
    }

    [Fact]
    public void GetCoverImage_ImageFormat_NoSpecials()
    {
        var volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("2", new List<Chapter>()
            {
                EntityFactory.CreateChapter("3", false),
                EntityFactory.CreateChapter("4", false),
            }),
            EntityFactory.CreateVolume("1", new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("0", true),
            }),
        };

        Assert.Equal(volumes[1].Name, volumes.GetCoverImage(MangaFormat.Image).Name);
    }


    #endregion
}
