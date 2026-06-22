namespace Kavita.Models.DTOs.KavitaPlus;

public sealed record KavitaPlusAuditStatsDto
{
    public int Events24H { get; init; }
    public int Failures24H { get; init; }
    public int UnresolvedMatchFailures { get; init; }
    public int MatchedSeriesCount { get; init; }
    public int TotalEligibleSeriesCount { get; init; }
    /// <summary>
    /// Series that are matched but whose cached metadata has expired and needs a refresh.
    /// The series is still considered matched — the data is just stale.
    /// </summary>
    public int StaleMatchesCount { get; init; }
    /// <summary>
    /// Series that Kavita+ returned "Unknown Series" for; they were attempted but could not be matched.
    /// These are not counted as matched and require manual intervention (fix match or set DontMatch).
    /// </summary>
    public int BlacklistedSeriesCount { get; init; }
    public int ScrobbleQueueCount { get; init; }
}
