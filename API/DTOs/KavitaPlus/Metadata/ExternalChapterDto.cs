using System;
using System.Collections.Generic;
using API.DTOs.SeriesDetail;

namespace API.DTOs.KavitaPlus.Metadata;

/// <summary>
/// Information about an individual issue/chapter/book from Kavita+
/// </summary>
public class ExternalChapterDto
{
    public string Title { get; set; }

    public string IssueNumber { get; set; }

    public decimal? CriticRating { get; set; }

    public decimal? UserRating { get; set; }

    public string? Summary { get; set; }

    public IList<string>? Writers { get; set; }

    public IList<string>? Artists { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? Publisher { get; set; }

    public string? CoverImageUrl { get; set; }

    public string? IssueUrl { get; set; }

    public IList<UserReviewDto> CriticReviews { get; set; }
    public IList<UserReviewDto> UserReviews { get; set; }
}
