using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums;

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

    // SeriesMetadataProviderOverrideSet
    public MetadataProvider? PreviousProvider { get; init; }
    public MetadataProvider? NewProvider { get; init; }
    /// <summary>
    /// False when the Series fell back to its Library's default provider
    /// </summary>
    public bool? IsProviderOverride { get; init; }

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { MatchedName = p.MatchedName, Before = p.Before, After = p.After };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchClearedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { MatchedName = p.MatchedName };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchFailureParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { Reason = p.Reason };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchDontMatchParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto { DontMatch = p.DontMatch };

    public static KavitaPlusAuditMatchDetailsDto? From(AuditLogMatchProviderOverrideParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMatchDetailsDto
        {
            PreviousProvider = p.PreviousProvider, NewProvider = p.NewProvider, IsProviderOverride = p.IsOverride
        };
}
