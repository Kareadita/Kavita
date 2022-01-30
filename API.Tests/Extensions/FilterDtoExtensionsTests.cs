using System;
using System.Collections.Generic;
using API.DTOs.Filtering;
using API.Entities.Enums;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions;

public class FilterDtoExtensionsTests
{
    [Fact]
    public void GetSqlFilter_ShouldReturnAllFormats()
    {
        var filter = new FilterDto()
        {
            Formats = null
        };

        Assert.Equal(Enum.GetValues<MangaFormat>(), filter.GetSqlFilter());
    }

    [Fact]
    public void GetSqlFilter_ShouldReturnAllFormats2()
    {
        var filter = new FilterDto()
        {
            Formats = new List<MangaFormat>()
        };

        Assert.Equal(Enum.GetValues<MangaFormat>(), filter.GetSqlFilter());
    }

    [Fact]
    public void GetSqlFilter_ShouldReturnJust2()
    {
        var formats = new List<MangaFormat>()
        {
            MangaFormat.Archive, MangaFormat.Epub
        };
        var filter = new FilterDto()
        {
            Formats = formats
        };

        Assert.Equal(formats, filter.GetSqlFilter());
    }
}
