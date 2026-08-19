namespace Kavita.Models.DTOs.KavitaPlus.Audit;

public sealed record KavitaPlusMyAuditStatsDto
{
    public int Events24H { get; init; }
    public int Failures24H { get; init; }
    public int ScrobbleQueueCount { get; init; }
}
