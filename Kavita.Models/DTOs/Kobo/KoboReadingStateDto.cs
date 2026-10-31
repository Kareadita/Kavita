using System;
using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboWrappedReadingStateDto
{
    public KoboReadingStateDto ReadingState { get; set; }
}

public sealed record KoboReadingStateDto
{
    public required string EntitlementId { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    /// <summary>
    /// CW: Equal to LastModified
    /// </summary>
    public DateTime PriorityTimestamp { get; set; }

    public KoboBookmarkDto CurrentBookmark { get; set; }
    public KoboStatisticsDto Statistics { get; set; }
    public KoboStatusInfoDto StatusInfo { get; set; }
}

public sealed record KoboBookmarkDto
{
    public DateTime LastModified { get; set; }
    public float ProgressPercent { get; set; }
    public float? ContentSourceProgressPercent { get; set; } = null;
    public KoboLocationDto? Location { get; set; } = null;
}

public sealed record KoboLocationDto
{
    public string? Value { get; set; } = null;
    public string? Type { get; set; } = "KoboSpan";
    public string Source { get; set; }
}

public sealed record KoboStatisticsDto
{
    public DateTime LastModified { get; set; }
    public int? RemainingTimeMinutes { get; set; } = null;
    public int? SpentReadingMinutes { get; set; } = null;
}

public sealed record KoboStatusInfoDto
{
    public DateTime LastModified { get; set; }
    public KoboReadStatus Status { get; set; }
    public int? TimesStartedReading { get; set; } = null;
    public int? LastTimeFinished { get; set; } = null;
    public int? LastTimeStartedReading { get; set; } = null;
}

public enum KoboReadStatus
{
    ReadyToRead,
    Finished,
    Reading,
}
