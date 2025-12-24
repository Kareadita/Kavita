using API.Data.Metadata;
using API.Entities.Enums;
using Xunit;

namespace API.Tests.Entities;

public class ComicInfoTests
{
    #region ConvertAgeRatingToEnum

    [Theory]
    [InlineData("G", AgeRating.G)]
    [InlineData("Everyone", AgeRating.Everyone)]
    [InlineData("Teen", AgeRating.Teen)]
    [InlineData("Adults Only 18+", AgeRating.AdultsOnly)]
    [InlineData("Early Childhood", AgeRating.EarlyChildhood)]
    [InlineData("Everyone 10+", AgeRating.Everyone10Plus)]
    [InlineData("M", AgeRating.Mature)]
    [InlineData("MA15+", AgeRating.Mature15Plus)]
    [InlineData("Mature 17+", AgeRating.Mature17Plus)]
    [InlineData("Rating Pending", AgeRating.RatingPending)]
    [InlineData("X18+", AgeRating.X18Plus)]
    [InlineData("Kids to Adults", AgeRating.KidsToAdults)]
    [InlineData("NotValid", AgeRating.Unknown)]
    [InlineData("PG", AgeRating.PG)]
    [InlineData("R18+", AgeRating.R18Plus)]
    public void ConvertAgeRatingToEnum_ShouldConvertCorrectly(string input, AgeRating expected)
    {
        Assert.Equal(expected, ComicInfo.ConvertAgeRatingToEnum(input));
    }

    [Fact]
    public void ConvertAgeRatingToEnum_ShouldCompareCaseInsensitive()
    {
        Assert.Equal(AgeRating.RatingPending, ComicInfo.ConvertAgeRatingToEnum("rating pending"));
    }
    #endregion

    #region CalculatedCount

    [Fact]
    public void CalculatedCount_ReturnsVolumeCount()
    {
        var ci = new ComicInfo()
        {
            Number = "5",
            Volume = "10",
            Count = 10
        };

        Assert.Equal(5, ci.CalculatedCount());
    }

    [Fact]
    public void CalculatedCount_ReturnsNoCountWhenCountNotSet()
    {
        var ci = new ComicInfo()
        {
            Number = "5",
            Volume = "10",
            Count = 0
        };

        Assert.Equal(5, ci.CalculatedCount());
    }

    [Fact]
    public void CalculatedCount_ReturnsNumberCount()
    {
        var ci = new ComicInfo()
        {
            Number = "5",
            Volume = "",
            Count = 10
        };

        Assert.Equal(5, ci.CalculatedCount());
    }

    [Fact]
    public void CalculatedCount_ReturnsNumberCount_OnlyWholeNumber()
    {
        var ci = new ComicInfo()
        {
            Number = "5.7",
            Volume = "",
            Count = 10
        };

        Assert.Equal(5, ci.CalculatedCount());
    }


    #endregion

    #region ASIN/ISBN/GTIN

    [Theory]
    [InlineData("0-306-40615-2")] // ISBN-10
    [InlineData("978-0-306-40615-7")] // ISBN-13
    [InlineData("99921-58-10-7")]
    [InlineData("85-359-0277-5")]
    public void IsValid(string code)
    {
        // Note: ASIN's starting with "B0" are not able to be converted to ISBN
        Assert.Equal(code, ComicInfo.ParseGtin(code));
    }

    [Theory]
    [InlineData("001234567890")]
    [InlineData("9504000059437 ")]
    public void IsInvalid(string code)
    {
        Assert.Equal(string.Empty, ComicInfo.ParseGtin(code));
    }
    #endregion
}
