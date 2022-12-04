using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services.Tasks.Scanner;
using Xunit;

namespace API.Tests.Helpers;

public class SeriesHelperTests
{
    #region FindSeries
    [Fact]
    public void FindSeries_ShouldFind_SameFormat()
    {
        var series = DbFactory.Series("Darker than Black");
        series.OriginalName = "Something Random";
        series.Format = MangaFormat.Archive;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black",
            NormalizedName = "Darker than Black".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black".ToLower(),
            NormalizedName = "Darker than Black".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black".ToUpper(),
            NormalizedName = "Darker than Black".Normalize()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_NullName()
    {
        var series = DbFactory.Series("Darker than Black");
        series.OriginalName = null;
        series.Format = MangaFormat.Archive;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black",
            NormalizedName = "Darker than Black".Normalize()
        }));
    }

    [Fact]
    public void FindSeries_ShouldNotFind_WrongFormat()
    {
        var series = DbFactory.Series("Darker than Black");
        series.OriginalName = "Something Random";
        series.Format = MangaFormat.Archive;
        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black",
            NormalizedName = "Darker than Black".Normalize()
        }));

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black".ToLower(),
            NormalizedName = "Darker than Black".Normalize()
        }));

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black".ToUpper(),
            NormalizedName = "Darker than Black".Normalize()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_UsingOriginalName()
    {
        var series = DbFactory.Series("Darker than Black");
        series.OriginalName = "Something Random";
        series.Format = MangaFormat.Image;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random",
            NormalizedName = "Something Random".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToLower(),
            NormalizedName = "Something Random".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToUpper(),
            NormalizedName = "Something Random".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "SomethingRandom".ToUpper(),
            NormalizedName = "SomethingRandom".Normalize()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_UsingLocalizedName()
    {
        var series = DbFactory.Series("Darker than Black");
        series.LocalizedName = "Something Random";
        series.Format = MangaFormat.Image;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random",
            NormalizedName = "Something Random".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToLower(),
            NormalizedName = "Something Random".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToUpper(),
            NormalizedName = "Something Random".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "SomethingRandom".ToUpper(),
            NormalizedName = "SomethingRandom".Normalize()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_UsingLocalizedName_2()
    {
        var series = DbFactory.Series("My Dress-Up Darling");
        series.LocalizedName = "Sono Bisque Doll wa Koi wo Suru";
        series.Format = MangaFormat.Archive;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "My Dress-Up Darling",
            NormalizedName = "My Dress-Up Darling".Normalize()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Sono Bisque Doll wa Koi wo Suru".ToLower(),
            NormalizedName = "Sono Bisque Doll wa Koi wo Suru".Normalize()
        }));
    }
    #endregion

    [Fact]
    public void RemoveMissingSeries_Should_RemoveSeries()
    {
        var existingSeries = new List<Series>()
        {
            EntityFactory.CreateSeries("Darker than Black Vol 1"),
            EntityFactory.CreateSeries("Darker than Black"),
            EntityFactory.CreateSeries("Beastars"),
        };
        var missingSeries = new List<Series>()
        {
            EntityFactory.CreateSeries("Darker than Black Vol 1"),
        };
        existingSeries = SeriesHelper.RemoveMissingSeries(existingSeries, missingSeries, out var removeCount).ToList();

        Assert.DoesNotContain(missingSeries[0].Name, existingSeries.Select(s => s.Name));
        Assert.Equal(missingSeries.Count, removeCount);
    }
}
