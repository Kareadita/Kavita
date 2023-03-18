﻿using API.Data;
using API.Extensions;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Entities;

/// <summary>
/// Tests for <see cref="API.Entities.Series"/>
/// </summary>
public class SeriesTest
{
    [Theory]
    [InlineData("Darker than Black")]
    public void CreateSeries(string name)
    {
        var key = name.ToNormalized();
        var series = new SeriesBuilder(name).Build();
        Assert.Equal(0, series.Id);
        Assert.Equal(0, series.Pages);
        Assert.Equal(name, series.Name);
        Assert.Null(series.CoverImage);
        Assert.Equal(name, series.LocalizedName);
        Assert.Equal(name, series.SortName);
        Assert.Equal(name, series.OriginalName);
        Assert.Equal(key, series.NormalizedName);
    }
}
