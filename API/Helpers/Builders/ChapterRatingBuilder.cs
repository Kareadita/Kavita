using System;
using API.Entities;
using API.Entities.Enums;

namespace API.Helpers.Builders;

#nullable enable
public class ChapterRatingBuilder
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

    public ChapterRatingBuilder WithVolumeId(int volumeId)
    {
        _rating.VolumeId = volumeId;
        return this;
    }

    public ChapterRatingBuilder WithRating(int rating)
    {
        _rating.Rating = Math.Clamp(rating, 0, 5);
        _rating.HasBeenRated = true;
        return this;
    }

    public ChapterRatingBuilder WithReview(string review)
    {
        _rating.Review = review;
        return this;
    }

    public ChapterRatingBuilder WithProvider(RatingProvider provider)
    {
        _rating.Provider = provider;
        return this;
    }


}
