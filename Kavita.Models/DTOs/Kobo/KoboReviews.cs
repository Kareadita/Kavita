#nullable enable
using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboReviewsResponseDto
{
    public int CurrentPageIndex { get; set; }
    public string Cursor { get; set; }
    public List<KoboReviewDto> Items { get; set; } = [];
    public Dictionary<string, KoboReviewSummaryDto> ReviewSummary { get; set; } = new();
    public int TotalPageCount { get; set; }
}

public sealed record KoboReviewDto
{
    public string AuthorDisplayName { get; set; }
    public string Body { get; set; }
    public DateTime CreationDate { get; set; }
    public string CrossRevisionId { get; set; }
    public int Dislikes { get; set; }
    public string KoboId { get; set; }
    public int Likes { get; set; }
    public string ModerationStatus { get; set; } = "Approved";
    public string ProductId { get; set; }
    public string PublicationId { get; set; }
    public int Rating { get; set; }
    public string RevisionId { get; set; }
    public string Title { get; set; }
}

public sealed record KoboReviewSummaryDto
{
    public double AvgRating { get; set; }
    public int NumberOfReviews { get; set; }
    public int OpinionCount { get; set; }
    public Dictionary<string, int> RatingHistogram { get; set; } = new();
}

public sealed record KoboSubmitReviewDto
{
    public string AuthorDisplayName { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public int Rating { get; set; }
}
