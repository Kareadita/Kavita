using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.History;

namespace Kavita.Models.DTOs.KavitaPlus.Audit;
#nullable enable

/// <summary>
/// The payload types each <see cref="KavitaPlusEventType"/> is written with
/// </summary>
public sealed class KavitaPlusAuditPayloads : IExportJsonTypeSelector
{
    public IReadOnlyList<string> Discriminators => [nameof(KavitaPlusAuditLog.EventType)];
    public IReadOnlyList<Type> AllTypes => All;

    public IReadOnlyList<Type> TypesFor(IReadOnlyList<string?> values) =>
        int.TryParse(values[0], out var eventType) ? TypesFor((KavitaPlusEventType) eventType) ?? [] : [];

    /// <returns>Null when the event type isn't mapped yet, empty when it never carries a payload</returns>
    public static IReadOnlyList<Type>? TypesFor(KavitaPlusEventType eventType) => eventType switch
    {
        KavitaPlusEventType.SeriesMatched => [typeof(AuditLogMatchedParamsDto)],
        KavitaPlusEventType.SeriesMatchFailed or KavitaPlusEventType.SeriesBlacklisted => [typeof(AuditLogMatchFailureParamsDto)],
        KavitaPlusEventType.SeriesMatchFixed => [typeof(AuditLogMatchClearedParamsDto)],
        KavitaPlusEventType.SeriesDontMatchSet => [typeof(AuditLogMatchDontMatchParamsDto)],
        KavitaPlusEventType.SeriesMetadataProviderOverrideSet => [typeof(AuditLogMatchProviderOverrideParamsDto)],

        KavitaPlusEventType.MetadataFetched => [typeof(AuditLogMetadataFetchParamsDto)],
        KavitaPlusEventType.MetadataUpdated or KavitaPlusEventType.ChapterMetadataUpdated => [typeof(AuditLogMetadataChangesParamsDto)],
        KavitaPlusEventType.CoverUpdated => [typeof(AuditLogSeriesCoverParamsDto)],
        KavitaPlusEventType.ChapterCoverUpdated => [typeof(AuditLogChapterCoverParamsDto)],
        KavitaPlusEventType.VolumeCoverUpdated => [typeof(AuditLogVolumeCoverParamsDto)],
        KavitaPlusEventType.PersonCoverUpdated => [typeof(AuditLogPersonCoverParamsDto)],
        KavitaPlusEventType.PersonAliasAdded => [typeof(AuditLogPersonAliasParamsDto)],

        KavitaPlusEventType.CollectionSynced => [typeof(AuditLogCollectionSyncedParamsDto)],
        KavitaPlusEventType.CollectionItemAdded => [typeof(AuditLogCollectionItemParamsDto)],

        KavitaPlusEventType.ScrobbleEventCreated or KavitaPlusEventType.ScrobbleEventUpdated or KavitaPlusEventType.ScrobbleEventSent
            or KavitaPlusEventType.ScrobbleEventFailed or KavitaPlusEventType.ScrobbleRateLimitHit or KavitaPlusEventType.ScrobbleEventSkipped
            or KavitaPlusEventType.ScrobbleHoldRemoved or KavitaPlusEventType.ScrobbleHoldAdded => [typeof(AuditLogScrobbleParamsDto)],

        // Smart collection sync and want-to-read sync share these events
        KavitaPlusEventType.SyncStarted => [typeof(AuditLogCollectionStartedParamsDto), typeof(AuditLogWantToReadSyncParamsDto)],
        KavitaPlusEventType.SyncFailed => [typeof(AuditLogCollectionFailedParamsDto), typeof(AuditLogWantToReadSyncParamsDto)],
        KavitaPlusEventType.SyncCompleted => [typeof(AuditLogWantToReadSyncCompletedParamsDto)],

        KavitaPlusEventType.SystemTokenRefresh => [typeof(AuditLogSystemTokenRefreshParamsDto)],
        KavitaPlusEventType.SystemProviderInfoSync => [typeof(AuditLogSystemProviderInfoSyncParamsDto)],
        _ => null,
    };

    private static readonly IReadOnlyList<Type> All = Enum.GetValues<KavitaPlusEventType>()
        .SelectMany(e => TypesFor(e) ?? [])
        .Distinct()
        .ToList();
}
