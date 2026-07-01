using Kavita.Models.DTOs.KavitaPlus.Audit;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Match-specific context surfaced on a Kavita+ audit entry.
/// Projected from AuditLogMatch*ParamsDtos based on EventType.
/// Not returned directly by the API - each From() overload maps one source type.
/// </summary>
public sealed record KavitaPlusAuditMatchDetailsDto
{
    // SeriesMatched, SeriesMatchCleared
    public string? MatchedName { get; init; }

    // SeriesMatched - external ID snapshots before and after the match
    public AuditLogMatchExternalIdsParamsDto? Before { get; init; }
    public AuditLogMatchExternalIdsParamsDto? After { get; init; }

    // SeriesMatchFailed, SeriesBlacklisted
    public string? Reason { get; init; }

    // SeriesDontMatchSet
    public bool? DontMatch { get; init; }

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { MatchedName = p.MatchedName, Before = p.Before, After = p.After };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchClearedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { MatchedName = p.MatchedName };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchFailureParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { Reason = p.Reason };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchDontMatchParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { DontMatch = p.DontMatch };
}
