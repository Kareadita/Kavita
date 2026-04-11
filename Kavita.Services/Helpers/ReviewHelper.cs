using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.SeriesDetail;

namespace Kavita.Services.Helpers;

public static class ReviewHelper
{
    public static IEnumerable<UserReviewDto> SelectSpectrumOfReviews(IList<UserReviewDto> reviews)
    {
        IList<UserReviewDto> externalReviews;
        var totalReviews = reviews.Count;

        if (totalReviews > 10)
        {
            var stepSize = Math.Max((totalReviews - 4) / 8, 1);

            var selectedReviews = new List<UserReviewDto>()
            {
                reviews[0],
                reviews[1],
            };
            for (var i = 2; i < totalReviews - 2; i += stepSize)
            {
                selectedReviews.Add(reviews[i]);

                if (selectedReviews.Count >= 8)
                    break;
            }

            selectedReviews.Add(reviews[totalReviews - 2]);
            selectedReviews.Add(reviews[totalReviews - 1]);

            externalReviews = selectedReviews;
        }
        else
        {
            externalReviews = reviews;
        }

        return externalReviews.OrderByDescending(r => r.Score);
    }

}
