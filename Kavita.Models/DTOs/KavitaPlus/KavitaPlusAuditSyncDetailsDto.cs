using Kavita.Models.DTOs.KavitaPlus.Audit;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Sync-specific context surfaced on a Kavita+ audit entry.
/// Projected from AuditLogSync*ParamsDtos based on EventType.
/// </summary>
public sealed record KavitaPlusAuditSyncDetailsDto
{
    // CollectionSynced
    public string? CollectionName { get; init; }
    public string? StackId { get; init; }
    public int? ItemCount { get; init; }
    public int? MissingCount { get; init; }

    // CollectionItemAdded
    public string? SeriesName { get; init; }
    public int? SeriesId { get; init; }

    // SyncCompleted (WantToRead)
    public string? UserName { get; init; }
    public bool? HasMal { get; init; }
    public bool? HasAniList { get; init; }
    public int? SeriesMatched { get; init; }

    public static KavitaPlusAuditSyncDetailsDto? From(AuditLogCollectionSyncedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditSyncDetailsDto { CollectionName = p.CollectionName, StackId = p.StackId, ItemCount = p.ItemCount, MissingCount = p.MissingCount };

    public static KavitaPlusAuditSyncDetailsDto? From(AuditLogCollectionItemParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditSyncDetailsDto { CollectionName = p.CollectionName, SeriesName = p.SeriesName, SeriesId = p.SeriesId };

    public static KavitaPlusAuditSyncDetailsDto? From(AuditLogWantToReadSyncCompletedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditSyncDetailsDto { UserName = p.UserName, HasMal = p.HasMal, HasAniList = p.HasAniList, SeriesMatched = p.SeriesMatched };

    public static KavitaPlusAuditSyncDetailsDto? From(AuditLogCollectionStartedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditSyncDetailsDto { CollectionName = p.CollectionName, StackId = p.StackId, ItemCount = p.TotalItems };

    public static KavitaPlusAuditSyncDetailsDto? From(AuditLogCollectionFailedParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditSyncDetailsDto { CollectionName = p.CollectionName };

    public static KavitaPlusAuditSyncDetailsDto? From(AuditLogWantToReadSyncParamsDto? p) =>
        p is null ? null : new KavitaPlusAuditSyncDetailsDto { UserName = p.UserName, HasMal = p.HasMal, HasAniList = p.HasAniList };
}
