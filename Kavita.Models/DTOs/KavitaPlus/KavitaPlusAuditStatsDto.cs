namespace Kavita.Models.DTOs.KavitaPlus;

public sealed record KavitaPlusAuditStatsDto(
    int Events24h,
    int Failures24h,
    int UnresolvedMatchFailures,
    int MatchedSeriesCount,
    int TotalEligibleSeriesCount,
    int ScrobbleQueueCount
);
