#nullable enable
using System;
using API.Entities;
using API.Entities.User;

namespace API.Helpers.Builders;

public class ChapterRatingBuilder : IEntityBuilder<AppUserChapterRating>
{
    private readonly AppUserChapterRating _rating;
    public AppUserChapterRating Build() => _rating;

    public ChapterRatingBuilder(AppUserChapterRating? rating = null)
    {
        _rating = rating ?? new AppUserChapterRating();
    }

    public ChapterRatingBuilder WithSeriesId(int seriesId)
    {
        _rating.SeriesId = seriesId;
        return this;
    }

    public ChapterRatingBuilder WithChapterId(int chapterId)
    {
        _rating.ChapterId = chapterId;
        return this;
    }

    public ChapterRatingBuilder WithRating(int rating)
    {
        _rating.Rating = Math.Clamp(rating, 0, 5);
        return this;
    }

    public ChapterRatingBuilder WithBody(string body)
    {
        _rating.Review = body;
        return this;
    }
}
