namespace Kavita.Models.DTOs.KavitaPlus;

public sealed record KavitaPlusAuditStatsDto
{
    public int Events24h { get; init; }
    public int Failures24h { get; init; }
    public int UnresolvedMatchFailures { get; init; }
    public int MatchedSeriesCount { get; init; }
    public int TotalEligibleSeriesCount { get; init; }
    public int ScrobbleQueueCount { get; init; }
}
