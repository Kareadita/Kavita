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
            NormalizedName = API.Parser.Parser.Normalize("Darker than Black")
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black".ToLower(),
            NormalizedName = API.Parser.Parser.Normalize("Darker than Black")
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black".ToUpper(),
            NormalizedName = API.Parser.Parser.Normalize("Darker than Black")
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
            NormalizedName = API.Parser.Parser.Normalize("Darker than Black")
        }));

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black".ToLower(),
            NormalizedName = API.Parser.Parser.Normalize("Darker than Black")
        }));

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black".ToUpper(),
            NormalizedName = API.Parser.Parser.Normalize("Darker than Black")
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
            NormalizedName = API.Parser.Parser.Normalize("Something Random")
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToLower(),
            NormalizedName = API.Parser.Parser.Normalize("Something Random")
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToUpper(),
            NormalizedName = API.Parser.Parser.Normalize("Something Random")
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "SomethingRandom".ToUpper(),
            NormalizedName = API.Parser.Parser.Normalize("SomethingRandom")
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
