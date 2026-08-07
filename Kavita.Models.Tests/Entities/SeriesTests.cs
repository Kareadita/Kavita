using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Tests.Entities;

public class SeriesTests
{
    private static Series CreateSeries(MetadataProvider libraryProvider, MetadataProvider? overrideProvider)
    {
        var series = new SeriesBuilder("Test Series").Build();
        series.Library = new Library
        {
            Name = "Test Library",
            MetadataProvider = libraryProvider
        };
        series.MetadataProviderOverride = overrideProvider;
        return series;
    }

    [Fact]
    public void GetEffectiveMetadataProvider_ReturnsLibraryDefault_WhenNoOverrideSet()
    {
        var series = CreateSeries(MetadataProvider.Mangabaka, null);

        Assert.Equal(MetadataProvider.Mangabaka, series.GetEffectiveMetadataProvider());
    }

    [Fact]
    public void GetEffectiveMetadataProvider_ReturnsOverride_WhenSet()
    {
        var series = CreateSeries(MetadataProvider.Mangabaka, MetadataProvider.Hardcover);

        Assert.Equal(MetadataProvider.Hardcover, series.GetEffectiveMetadataProvider());
    }

    [Fact]
    public void GetEffectiveMetadataProvider_OverrideCanMatchLibraryDefault_WithoutChangingResult()
    {
        var series = CreateSeries(MetadataProvider.ComicBookRoundup, MetadataProvider.ComicBookRoundup);

        Assert.Equal(MetadataProvider.ComicBookRoundup, series.GetEffectiveMetadataProvider());
    }

    [Fact]
    public void GetEffectiveMetadataProvider_ClearingOverride_FallsBackToLibraryDefault()
    {
        var series = CreateSeries(MetadataProvider.Hardcover, MetadataProvider.ComicBookRoundup);
        Assert.Equal(MetadataProvider.ComicBookRoundup, series.GetEffectiveMetadataProvider());

        series.MetadataProviderOverride = null;

        Assert.Equal(MetadataProvider.Hardcover, series.GetEffectiveMetadataProvider());
    }
}
