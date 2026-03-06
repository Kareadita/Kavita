using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Models.Builders;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Repositories;

public class ExternalSeriesMetadataRepositoryTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    [Fact]
    public async Task NeedsDataRefresh_WhenValidUntilIsInThePast_ReturnsTrue()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series = context.Series.First(s => s.Name == "series0");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(-1) // expired yesterday
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(series.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task NeedsDataRefresh_WhenValidUntilIsInTheFuture_ReturnsFalse()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series = context.Series.First(s => s.Name == "series0");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(7) // valid for another week
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(series.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task NeedsDataRefresh_OnlyChecksRequestedSeries()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .WithSeries(new SeriesBuilder("series1").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series0 = context.Series.First(s => s.Name == "series0");
        var series1 = context.Series.First(s => s.Name == "series1");

        context.ExternalSeriesMetadata.AddRange(
            new ExternalSeriesMetadata { SeriesId = series0.Id, ValidUntilUtc = DateTime.UtcNow.AddDays(-1) },
            new ExternalSeriesMetadata { SeriesId = series1.Id, ValidUntilUtc = DateTime.UtcNow.AddDays(7) }
        );
        await context.SaveChangesAsync();

        var staleSeries  = await unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(series0.Id);
        var freshSeries  = await unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(series1.Id);

        Assert.True(staleSeries);
        Assert.False(freshSeries);
    }

    [Fact]
    public async Task GetSeriesDetailPlusDto_WithRatings_ReturnsAllRatings()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series = context.Series.First(s => s.Name == "series0");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(7),
            ExternalRatings = new List<ExternalRating>
            {
                new() { Provider = ScrobbleProvider.AniList, AverageScore = 85, FavoriteCount = 1000 },
                new() { Provider = ScrobbleProvider.Mal, AverageScore = 90, FavoriteCount = 2000 }
            }
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(series.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result.Ratings?.Count());
    }

    [Fact]
    public async Task GetSeriesDetailPlusDto_WithReviews_ReturnsReviewsSortedByScoreDescending()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series = context.Series.First(s => s.Name == "series0");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(7),
            ExternalReviews = new List<ExternalReview>
            {
                new() { Score = 60, Body = "Decent",    Username = "user1", Provider = ScrobbleProvider.AniList, BodyJustText = string.Empty},
                new() { Score = 95, Body = "Excellent", Username = "user2", Provider = ScrobbleProvider.AniList, BodyJustText = string.Empty },
                new() { Score = 80, Body = "Good",      Username = "user3", Provider = ScrobbleProvider.AniList, BodyJustText = string.Empty }
            }
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(series.Id);

        Assert.NotNull(result);
        var reviews = result.Reviews.ToList();
        Assert.Equal(3, reviews.Count);
        Assert.Equal(95, reviews[0].Score);
        Assert.Equal(80, reviews[1].Score);
        Assert.Equal(60, reviews[2].Score);
        Assert.All(reviews, r => Assert.True(r.IsExternal));
    }

    [Fact]
    public async Task GetSeriesDetailPlusDto_WithRecommendations_SplitsOwnedAndExternalCorrectly()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .WithSeries(new SeriesBuilder("owned-rec-series").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series       = context.Series.First(s => s.Name == "series0");
        var ownedRecSeries = context.Series.First(s => s.Name == "owned-rec-series");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(7),
            ExternalRecommendations = new List<ExternalRecommendation>
            {
                // owned — has a SeriesId pointing to an existing series
                new() { SeriesId = ownedRecSeries.Id, Name = ownedRecSeries.Name, Provider = ScrobbleProvider.AniList, CoverUrl = string.Empty, Url = string.Empty},
                // external — no SeriesId (not in library)
                new() { SeriesId = null,              Name = "External Rec 1",   Provider = ScrobbleProvider.AniList, CoverUrl = string.Empty, Url = string.Empty },
                new() { SeriesId = null,              Name = "External Rec 2",   Provider = ScrobbleProvider.AniList, CoverUrl = string.Empty, Url = string.Empty }
            }
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(series.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.Recommendations);

        Assert.Single(result.Recommendations.OwnedSeries);
        Assert.Equal(ownedRecSeries.Name, result.Recommendations.OwnedSeries[0].Name);

        Assert.Equal(2, result.Recommendations.ExternalSeries.Count());
        Assert.Contains(result.Recommendations.ExternalSeries, r => r.Name == "External Rec 1");
        Assert.Contains(result.Recommendations.ExternalSeries, r => r.Name == "External Rec 2");
    }

    [Fact]
    public async Task GetSeriesDetailPlusDto_WithNoRatingsOrReviews_ReturnsEmptyCollections()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series = context.Series.First(s => s.Name == "series0");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(7),
            ExternalRatings = new List<ExternalRating>(),
            ExternalReviews = new List<ExternalReview>(),
            ExternalRecommendations = new List<ExternalRecommendation>()
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(series.Id);

        Assert.NotNull(result);
        Assert.Empty(result.Ratings ?? []);
        Assert.Empty(result.Reviews);
        Assert.Empty(result.Recommendations?.OwnedSeries ?? []);
        Assert.Empty(result.Recommendations?.ExternalSeries ?? []);
    }

    [Fact]
    public async Task GetSeriesDetailPlusDto_OwnedRecommendations_AreSortedBySortNameAscending()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var lib = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("series0").Build())
            .WithSeries(new SeriesBuilder("Charlie").Build())
            .WithSeries(new SeriesBuilder("Alpha").Build())
            .WithSeries(new SeriesBuilder("Bravo").Build())
            .Build();
        context.Library.Add(lib);
        await context.SaveChangesAsync();

        var series  = context.Series.First(s => s.Name == "series0");
        var charlie = context.Series.First(s => s.Name == "Charlie");
        var alpha   = context.Series.First(s => s.Name == "Alpha");
        var bravo   = context.Series.First(s => s.Name == "Bravo");

        var metadata = new ExternalSeriesMetadata
        {
            SeriesId = series.Id,
            ValidUntilUtc = DateTime.UtcNow.AddDays(7),
            ExternalRecommendations = new List<ExternalRecommendation>
            {
                new() { SeriesId = charlie.Id, Name = "Charlie", Provider = ScrobbleProvider.AniList, CoverUrl = string.Empty, Url = string.Empty },
                new() { SeriesId = alpha.Id,   Name = "Alpha",   Provider = ScrobbleProvider.AniList, CoverUrl = string.Empty, Url = string.Empty },
                new() { SeriesId = bravo.Id,   Name = "Bravo",   Provider = ScrobbleProvider.AniList, CoverUrl = string.Empty, Url = string.Empty }
            }
        };
        context.ExternalSeriesMetadata.Add(metadata);
        await context.SaveChangesAsync();

        var result = await unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(series.Id);

        Assert.NotNull(result);
        var owned = result.Recommendations?.OwnedSeries ?? [];
        Assert.Equal(3, owned.Count);
        Assert.Equal("Alpha",   owned[0].Name);
        Assert.Equal("Bravo",   owned[1].Name);
        Assert.Equal("Charlie", owned[2].Name);
    }
}
