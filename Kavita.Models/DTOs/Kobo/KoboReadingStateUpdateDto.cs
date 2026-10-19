using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboReadingStateUpdateDto
{
    public List<KoboReadingStateDto> ReadingStates { get; set; } = [];
}

public sealed record KoboStateUpdateResultDto
{
    public KoboRequestResult RequestResult { get; set; }
    public List<KoboReadingStateUpdateResultDto> UpdateResults { get; set; } = [];

}

public sealed record KoboReadingStateUpdateResultDto
{
    public string EntitlementId { get; set; }
    public KoboWrappedResultDto CurrentBookmarkResult { get; set; }
    public KoboWrappedResultDto StatisticsResult { get; set; }
    public KoboWrappedResultDto StatusInfoResult { get; set; }

    public KoboReadingStateUpdateResultDto(KoboRequestResult result) : this(result, result, result)
    { }

    public KoboReadingStateUpdateResultDto(KoboRequestResult bookmark, KoboRequestResult stats,
        KoboRequestResult statusInfo)
    {
        CurrentBookmarkResult = new KoboWrappedResultDto(bookmark);
        StatisticsResult = new KoboWrappedResultDto(stats);
        StatusInfoResult = new KoboWrappedResultDto(statusInfo);
    }
}

public sealed record KoboWrappedResultDto
{
    public KoboRequestResult Result { get; set; }

    public KoboWrappedResultDto(KoboRequestResult result)
    {
        Result = result;
    }
}

public enum KoboRequestResult
{
    Success,
    Failure,
    Ignored,
}
