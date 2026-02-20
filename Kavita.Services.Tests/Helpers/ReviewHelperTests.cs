using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class ReviewHelperTests
{
    #region SelectSpectrumOfReviews Tests

    [Fact]
    public void SelectSpectrumOfReviews_WhenLessThan10Reviews_ReturnsAllReviews()
    {

        var reviews = CreateReviewList(8);

        // Act
        var result = ReviewHelper.SelectSpectrumOfReviews(reviews).ToList();

        // Assert
        Assert.Equal(8, result.Count);
        Assert.Equal(reviews, result.OrderByDescending(r => r.Score));
    }

    [Fact]
    public void SelectSpectrumOfReviews_WhenMoreThan10Reviews_Returns10Reviews()
    {

        var reviews = CreateReviewList(20);

        // Act
        var result = ReviewHelper.SelectSpectrumOfReviews(reviews).ToList();

        // Assert
        Assert.Equal(10, result.Count);
        Assert.Equal(reviews[0], result.First());
        Assert.Equal(reviews[19], result.Last());
    }

    [Fact]
    public void SelectSpectrumOfReviews_WithExactly10Reviews_ReturnsAllReviews()
    {

        var reviews = CreateReviewList(10);

        // Act
        var result = ReviewHelper.SelectSpectrumOfReviews(reviews).ToList();

        // Assert
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void SelectSpectrumOfReviews_WithLargeNumberOfReviews_ReturnsCorrectSpectrum()
    {

        var reviews = CreateReviewList(100);

        // Act
        var result = ReviewHelper.SelectSpectrumOfReviews(reviews).ToList();

        // Assert
        Assert.Equal(10, result.Count);
        Assert.Contains(reviews[0], result);
        Assert.Contains(reviews[1], result);
        Assert.Contains(reviews[98], result);
        Assert.Contains(reviews[99], result);
    }

    [Fact]
    public void SelectSpectrumOfReviews_WithEmptyList_ReturnsEmptyList()
    {

        var reviews = new List<UserReviewDto>();

        // Act
        var result = ReviewHelper.SelectSpectrumOfReviews(reviews).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SelectSpectrumOfReviews_ResultsOrderedByScoreDescending()
    {

        var reviews = new List<UserReviewDto>
        {
            new UserReviewDto { Tagline = "1", Score = 3 },
            new UserReviewDto { Tagline = "2", Score = 5 },
            new UserReviewDto { Tagline = "3", Score = 1 },
            new UserReviewDto { Tagline = "4", Score = 4 },
            new UserReviewDto { Tagline = "5", Score = 2 }
        };

        // Act
        var result = ReviewHelper.SelectSpectrumOfReviews(reviews).ToList();

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal(5, result[0].Score);
        Assert.Equal(4, result[1].Score);
        Assert.Equal(3, result[2].Score);
        Assert.Equal(2, result[3].Score);
        Assert.Equal(1, result[4].Score);
    }

    #endregion

    #region Helper Methods

    private static List<UserReviewDto> CreateReviewList(int count)
    {
        var reviews = new List<UserReviewDto>();
        for (var i = 0; i < count; i++)
        {
            reviews.Add(new UserReviewDto
            {
                Tagline = $"{i + 1}",
                Score = count - i // This makes them ordered by score descending initially
            });
        }
        return reviews;
    }

    #endregion
}

