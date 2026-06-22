using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums.Audit;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Extra context for non-diff Metadata events (cover updates, person operations, metadata fetches).
/// Projected from AuditLogSeriesCoverParamsDto, AuditLogChapterCoverParamsDto,
/// AuditLogPersonAliasParamsDto, AuditLogPersonCoverParamsDto, AuditLogMetadataFetchParamsDto.
/// </summary>
public sealed record KavitaPlusAuditMetadataExtrasDto
{
    // CoverUpdated, ChapterCoverUpdated, PersonCoverUpdated
    public string? CoverUrl { get; init; }

    // ChapterCoverUpdated
    public string? IssueNumber { get; init; }

    // PersonAliasAdded, PersonCoverUpdated
    public string? PersonName { get; init; }

    // PersonAliasAdded
    public string? AliasAdded { get; init; }

    // MetadataFetched - why the fetch fired
    public MetadataFetchTrigger? FetchTrigger { get; init; }

    public static KavitaPlusAuditMetadataExtrasDto? From(AuditLogSeriesCoverParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMetadataExtrasDto { CoverUrl = p.CoverUrl };

    public static KavitaPlusAuditMetadataExtrasDto? From(AuditLogChapterCoverParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMetadataExtrasDto { CoverUrl = p.CoverUrl, IssueNumber = p.IssueNumber };

    public static KavitaPlusAuditMetadataExtrasDto? From(AuditLogPersonAliasParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMetadataExtrasDto { PersonName = p.PersonName, AliasAdded = p.AliasAdded };

    public static KavitaPlusAuditMetadataExtrasDto? From(AuditLogPersonCoverParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMetadataExtrasDto { PersonName = p.PersonName, CoverUrl = p.ImageUrl };

    public static KavitaPlusAuditMetadataExtrasDto? From(AuditLogMetadataFetchParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditMetadataExtrasDto { FetchTrigger = p.Trigger };
}
