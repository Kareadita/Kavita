#nullable enable
using System;
using API.Entities;

namespace API.Helpers.Builders;

public class RatingBuilder : IEntityBuilder<AppUserRating>
{
    private readonly AppUserRating _rating;
    public AppUserRating Build() => _rating;

    public RatingBuilder(AppUserRating? rating = null)
    {
        _rating = rating ?? new AppUserRating();
    }

    public RatingBuilder WithSeriesId(int seriesId)
    {
        _rating.SeriesId = seriesId;
        return this;
    }

    public RatingBuilder WithRating(int rating)
    {
        _rating.Rating = Math.Clamp(rating, 0, 5);
        return this;
    }

    public RatingBuilder WithTagline(string? tagline)
    {
        if (string.IsNullOrEmpty(tagline)) return this;
        _rating.Tagline = tagline;
        return this;
    }

    public RatingBuilder WithBody(string body)
    {
        _rating.Review = body;
        return this;
    }
}
