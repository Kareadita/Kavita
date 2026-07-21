using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class SeriesHelperTests
{
    #region FindSeries
    [Fact]
    public void FindSeries_ShouldFind_SameFormat()
    {
        var series = new SeriesBuilder("Darker than Black").Build();
        series.OriginalName = "Something Random";
        series.Format = MangaFormat.Archive;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black",
            NormalizedName = "Darker than Black".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black".ToLower(),
            NormalizedName = "Darker than Black".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black".ToUpper(),
            NormalizedName = "Darker than Black".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_NullName()
    {
        var series = new SeriesBuilder("Darker than Black").Build();
        series.OriginalName = null;
        series.Format = MangaFormat.Archive;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Darker than Black",
            NormalizedName = "Darker than Black".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldNotFind_WrongFormat()
    {
        var series = new SeriesBuilder("Darker than Black").Build();
        series.OriginalName = "Something Random";
        series.NormalizedOriginalName = "Something Random".ToNormalized();
        series.Format = MangaFormat.Archive;
        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black",
            NormalizedName = "Darker than Black".ToNormalized()
        }));

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black".ToLower(),
            NormalizedName = "Darker than Black".ToNormalized()
        }));

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Darker than Black".ToUpper(),
            NormalizedName = "Darker than Black".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_UsingOriginalName()
    {
        var series = new SeriesBuilder("Darker than Black").Build();
        series.OriginalName = "Something Random";
        series.NormalizedOriginalName = "Something Random".ToNormalized();
        series.Format = MangaFormat.Image;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random",
            NormalizedName = "Something Random".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToLower(),
            NormalizedName = "Something Random".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToUpper(),
            NormalizedName = "Something Random".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "SomethingRandom".ToUpper(),
            NormalizedName = "SomethingRandom".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_UsingLocalizedName()
    {
        var series = new SeriesBuilder("Darker than Black").Build();
        series.LocalizedName = "Something Random";
        series.NormalizedLocalizedName = "Something Random".ToNormalized();
        series.Format = MangaFormat.Image;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random",
            NormalizedName = "Something Random".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToLower(),
            NormalizedName = "Something Random".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "Something Random".ToUpper(),
            NormalizedName = "Something Random".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Image,
            Name = "SomethingRandom".ToUpper(),
            NormalizedName = "SomethingRandom".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_UsingLocalizedName_2()
    {
        var series = new SeriesBuilder("My Dress-Up Darling").Build();
        series.LocalizedName = "Sono Bisque Doll wa Koi wo Suru";
        series.NormalizedLocalizedName = "Sono Bisque Doll wa Koi wo Suru".ToNormalized();
        series.Format = MangaFormat.Archive;
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "My Dress-Up Darling",
            NormalizedName = "My Dress-Up Darling".ToNormalized()
        }));

        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Sono Bisque Doll wa Koi wo Suru".ToLower(),
            NormalizedName = "Sono Bisque Doll wa Koi wo Suru".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldFind_AfterRename_ViaOriginalName()
    {
        // Simulate a UI/K+ rename: Name (and its NormalizedName) move away from the folder name,
        // but OriginalName still anchors to the on-disk folder.
        var series = new SeriesBuilder("Batman").Build();
        series.Format = MangaFormat.Archive;
        series.Name = "The Dark Knight";
        series.NormalizedName = "The Dark Knight".ToNormalized();
        // OriginalName remains "Batman"

        // The folder on disk still parses as "Batman"
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Batman",
            NormalizedName = "Batman".ToNormalized()
        }));

        // And the renamed Name still resolves via NormalizedName
        Assert.True(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "The Dark Knight",
            NormalizedName = "The Dark Knight".ToNormalized()
        }));
    }

    [Fact]
    public void FindSeries_ShouldNotFind_WhenNoNameMatches()
    {
        var series = new SeriesBuilder("Batman").Build();
        series.Format = MangaFormat.Archive;

        Assert.False(SeriesHelper.FindSeries(series, new ParsedSeries()
        {
            Format = MangaFormat.Archive,
            Name = "Superman",
            NormalizedName = "Superman".ToNormalized()
        }));
    }
    #endregion

    [Fact]
    public void RemoveMissingSeries_Should_RemoveSeries()
    {
        var existingSeries = new List<Series>()
        {
            new SeriesBuilder("Darker than Black Vol 1").Build(),
            new SeriesBuilder("Darker than Black").Build(),
            new SeriesBuilder("Beastars").Build(),
        };
        var missingSeries = new List<Series>()
        {
            new SeriesBuilder("Darker than Black Vol 1").Build(),
        };
        existingSeries = SeriesHelper.RemoveMissingSeries(existingSeries, missingSeries, out var removeCount).ToList();

        Assert.DoesNotContain(missingSeries[0].Name, existingSeries.Select(s => s.Name));
        Assert.Equal(missingSeries.Count, removeCount);
    }
}
